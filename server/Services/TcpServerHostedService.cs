using System.Net;
using System.Net.Sockets;
using Mingle.Server.Data;
using Mingle.Server.Protocol;
using Mingle.Server.Transport;

namespace Mingle.Server.Services;

public sealed class TcpServerHostedService(
    IConfiguration configuration,
    TcpMessageProcessor messageProcessor,
    IRealtimeConnectionRegistry realtimeRegistry,
    IUserRepository userRepository,
    ILogger<TcpServerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(5);
    private TcpListener? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpPort = int.TryParse(configuration["TCP_PORT"], out var parsedPort) ? parsedPort : 58081;

        _listener = new TcpListener(IPAddress.Any, tcpPort);
        _listener.Start();
        logger.LogInformation("TCP server listening on port {Port}", tcpPort);
        var heartbeatTask = Task.Run(() => HeartbeatLoopAsync(stoppingToken), stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = Task.Run(() => HandleClientAsync(client, stoppingToken), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid();
        var sendLock = new SemaphoreSlim(1, 1);

        try
        {
            using var stream = client.GetStream();
            var connection = new TcpClientConnection(
                connectionId,
                async (message, ct) =>
                {
                    var responseBytes = TcpMessageProcessor.Serialize(message);
                    await sendLock.WaitAsync(ct);
                    try
                    {
                        await TcpFrameCodec.WriteFrameAsync(stream, responseBytes, ct);
                    }
                    finally
                    {
                        sendLock.Release();
                    }
                });
            realtimeRegistry.Register(connection);

            while (!cancellationToken.IsCancellationRequested)
            {
                var payload = await TcpFrameCodec.ReadFrameAsync(stream, cancellationToken);
                if (payload is null)
                {
                    break;
                }

                var request = TcpMessageProcessor.Deserialize<ClientMessage>(payload);
                var response = await messageProcessor.ProcessAsync(request, connectionId, cancellationToken);
                await connection.SendAsync(response, cancellationToken);
            }
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "Invalid TCP frame received.");
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "TCP client disconnected.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected TCP connection error.");
        }
        finally
        {
            var transition = realtimeRegistry.Unregister(connectionId);
            if (transition.Success && transition.PresenceChanged && transition.UserId.HasValue)
            {
                if (!transition.IsOnline)
                {
                    await userRepository.TouchLastSeenAsync(transition.UserId.Value);
                }

                await realtimeRegistry.BroadcastPresenceAsync(
                    transition.UserId.Value,
                    isOnline: transition.IsOnline,
                    lastSeenAtUnixMs: transition.LastSeenAtUnixMs,
                    cancellationToken);
            }

            client.Close();
            sendLock.Dispose();
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            var connectionIds = realtimeRegistry.GetConnectionIds();
            foreach (var connectionId in connectionIds)
            {
                await realtimeRegistry.PushToConnectionAsync(
                    connectionId,
                    new ServerMessage
                    {
                        ProtocolVersion = TcpMessageProcessor.ProtocolVersion,
                        ServerPing = new ServerPing
                        {
                            UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        }
                    },
                    cancellationToken);
            }

            var staleTransitions = realtimeRegistry.SweepStaleConnections(now, HeartbeatTimeout);
            foreach (var transition in staleTransitions.Where(x => x.Success && x.PresenceChanged && x.UserId.HasValue))
            {
                if (!transition.IsOnline)
                {
                    await userRepository.TouchLastSeenAsync(transition.UserId!.Value);
                }

                await realtimeRegistry.BroadcastPresenceAsync(
                    transition.UserId!.Value,
                    transition.IsOnline,
                    transition.LastSeenAtUnixMs,
                    cancellationToken);
            }
        }
    }
}
