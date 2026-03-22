using System.Net;
using System.Net.Sockets;
using Mingle.Server.Protocol;
using Mingle.Server.Transport;

namespace Mingle.Server.Services;

public sealed class TcpServerHostedService(
    IConfiguration configuration,
    TcpMessageProcessor messageProcessor,
    ILogger<TcpServerHostedService> logger) : BackgroundService
{
    private TcpListener? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpPort = int.TryParse(configuration["TCP_PORT"], out var parsedPort) ? parsedPort : 58081;

        _listener = new TcpListener(IPAddress.Any, tcpPort);
        _listener.Start();
        logger.LogInformation("TCP server listening on port {Port}", tcpPort);

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
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                var payload = await TcpFrameCodec.ReadFrameAsync(stream, cancellationToken);
                if (payload is null)
                {
                    break;
                }

                var request = TcpMessageProcessor.Deserialize<ClientMessage>(payload);
                var response = await messageProcessor.ProcessAsync(request);
                var responseBytes = TcpMessageProcessor.Serialize(response);

                await TcpFrameCodec.WriteFrameAsync(stream, responseBytes, cancellationToken);
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
            client.Close();
        }
    }
}
