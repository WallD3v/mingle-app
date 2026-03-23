using System.Collections.Concurrent;
using Mingle.Server.Protocol;

namespace Mingle.Server.Services;

public interface IRealtimeConnectionRegistry
{
    void Register(TcpClientConnection connection);
    PresenceTransition Unregister(Guid connectionId);
    PresenceTransition Subscribe(Guid connectionId, Guid userId);
    bool IsUserOnline(Guid userId);
    Task BroadcastPresenceAsync(Guid changedUserId, bool isOnline, long lastSeenAtUnixMs, CancellationToken cancellationToken);
    Task PushMessageReceivedAsync(Guid recipientUserId, MessageReceived payload, CancellationToken cancellationToken);
    Task PushToUserAsync(Guid userId, ServerMessage message, CancellationToken cancellationToken);
}

public readonly record struct PresenceTransition(bool Success, Guid? UserId, bool PresenceChanged);

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

    public PresenceTransition Unregister(Guid connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
        {
            return new PresenceTransition(false, null, false);
        }

        if (!connection.UserId.HasValue)
        {
            return new PresenceTransition(true, null, false);
        }

        var becameOffline = RemoveUserConnection(connection.UserId.Value, connectionId);
        return new PresenceTransition(true, connection.UserId.Value, becameOffline);
    }

    public PresenceTransition Subscribe(Guid connectionId, Guid userId)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
        {
            return new PresenceTransition(false, null, false);
        }

        var wasOnline = IsUserOnline(userId);

        if (connection.UserId.HasValue)
        {
            RemoveUserConnection(connection.UserId.Value, connectionId);
        }

        connection.UserId = userId;
        var set = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, byte>());
        set[connectionId] = 0;
        return new PresenceTransition(true, userId, !wasOnline);
    }

    public bool IsUserOnline(Guid userId)
    {
        return _userConnections.TryGetValue(userId, out var connections) && !connections.IsEmpty;
    }

    public async Task BroadcastPresenceAsync(Guid changedUserId, bool isOnline, long lastSeenAtUnixMs, CancellationToken cancellationToken)
    {
        var message = new ServerMessage
        {
            ProtocolVersion = TcpMessageProcessor.ProtocolVersion,
            PresenceUpdate = new PresenceUpdate
            {
                UserId = changedUserId.ToString(),
                IsOnline = isOnline,
                LastSeenAtUnixMs = lastSeenAtUnixMs
            }
        };

        foreach (var onlineUserId in _userConnections.Keys.Where(x => x != changedUserId))
        {
            await PushToUserAsync(onlineUserId, message, cancellationToken);
        }
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

    private bool RemoveUserConnection(Guid userId, Guid connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var set))
        {
            set.TryRemove(connectionId, out _);
            if (set.IsEmpty)
            {
                _userConnections.TryRemove(userId, out _);
                return true;
            }
        }

        return false;
    }
}
