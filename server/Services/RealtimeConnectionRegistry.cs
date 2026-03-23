using System.Collections.Concurrent;
using Mingle.Server.Protocol;

namespace Mingle.Server.Services;

public interface IRealtimeConnectionRegistry
{
    void Register(TcpClientConnection connection);
    void Unregister(Guid connectionId);
    bool Subscribe(Guid connectionId, Guid userId);
    Task PushMessageReceivedAsync(Guid recipientUserId, MessageReceived payload, CancellationToken cancellationToken);
    Task PushToUserAsync(Guid userId, ServerMessage message, CancellationToken cancellationToken);
}

public sealed class TcpClientConnection(
    Guid connectionId,
    Func<ServerMessage, CancellationToken, Task> sendAsync)
{
    public Guid ConnectionId { get; } = connectionId;
    public Guid? UserId { get; set; }

    public Task SendAsync(ServerMessage message, CancellationToken cancellationToken)
    {
        return sendAsync(message, cancellationToken);
    }
}

public sealed class RealtimeConnectionRegistry : IRealtimeConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, TcpClientConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _userConnections = new();

    public void Register(TcpClientConnection connection)
    {
        _connections[connection.ConnectionId] = connection;
    }

    public void Unregister(Guid connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection) && connection.UserId.HasValue)
        {
            RemoveUserConnection(connection.UserId.Value, connectionId);
        }
    }

    public bool Subscribe(Guid connectionId, Guid userId)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
        {
            return false;
        }

        if (connection.UserId.HasValue)
        {
            RemoveUserConnection(connection.UserId.Value, connectionId);
        }

        connection.UserId = userId;
        var set = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, byte>());
        set[connectionId] = 0;
        return true;
    }

    public async Task PushMessageReceivedAsync(Guid recipientUserId, MessageReceived payload, CancellationToken cancellationToken)
    {
        await PushToUserAsync(recipientUserId, new ServerMessage
        {
            ProtocolVersion = TcpMessageProcessor.ProtocolVersion,
            MessageReceived = payload
        }, cancellationToken);
    }

    public async Task PushToUserAsync(Guid userId, ServerMessage message, CancellationToken cancellationToken)
    {
        if (!_userConnections.TryGetValue(userId, out var connections) || connections.IsEmpty)
        {
            return;
        }

        var dead = new List<Guid>();
        foreach (var connectionId in connections.Keys)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
            {
                dead.Add(connectionId);
                continue;
            }

            try
            {
                await connection.SendAsync(message, cancellationToken);
            }
            catch
            {
                dead.Add(connectionId);
            }
        }

        foreach (var id in dead)
        {
            Unregister(id);
        }
    }

    private void RemoveUserConnection(Guid userId, Guid connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var set))
        {
            set.TryRemove(connectionId, out _);
            if (set.IsEmpty)
            {
                _userConnections.TryRemove(userId, out _);
            }
        }
    }
}
