using System.Collections.Concurrent;
using Mingle.Server.Protocol;

namespace Mingle.Server.Services;

public interface IRealtimeConnectionRegistry
{
    void Register(TcpClientConnection connection);
    PresenceTransition Unregister(Guid connectionId);
    PresenceTransition Subscribe(Guid connectionId, Guid userId);
    PresenceTransition MarkHeartbeat(Guid connectionId, bool isAppForeground, DateTime utcNow);
    IReadOnlyList<PresenceTransition> SweepStaleConnections(DateTime utcNow, TimeSpan timeout);
    IReadOnlyList<Guid> GetConnectionIds();
    Task<bool> PushToConnectionAsync(Guid connectionId, ServerMessage message, CancellationToken cancellationToken);
    bool IsUserOnline(Guid userId);
    Task BroadcastPresenceAsync(Guid changedUserId, bool isOnline, long lastSeenAtUnixMs, CancellationToken cancellationToken);
    Task PushMessageReceivedAsync(Guid recipientUserId, MessageReceived payload, CancellationToken cancellationToken);
    Task PushToUserAsync(Guid userId, ServerMessage message, CancellationToken cancellationToken);
}

public readonly record struct PresenceTransition(bool Success, Guid? UserId, bool PresenceChanged, bool IsOnline, long LastSeenAtUnixMs);

public sealed class TcpClientConnection(
    Guid connectionId,
    Func<ServerMessage, CancellationToken, Task> sendAsync)
{
    public Guid ConnectionId { get; } = connectionId;
    public Guid? UserId { get; set; }
    public DateTime LastHeartbeatAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsAppForeground { get; set; } = true;

    public Task SendAsync(ServerMessage message, CancellationToken cancellationToken)
    {
        return sendAsync(message, cancellationToken);
    }
}

public sealed class RealtimeConnectionRegistry : IRealtimeConnectionRegistry
{
    private static readonly TimeSpan OnlineTtl = TimeSpan.FromSeconds(3);
    private readonly ConcurrentDictionary<Guid, TcpClientConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _userConnections = new();
    private readonly ConcurrentDictionary<Guid, bool> _onlineState = new();

    public void Register(TcpClientConnection connection)
    {
        _connections[connection.ConnectionId] = connection;
    }

    public PresenceTransition Unregister(Guid connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
        {
            return new PresenceTransition(false, null, false, false, 0);
        }

        if (!connection.UserId.HasValue)
        {
            return new PresenceTransition(true, null, false, false, 0);
        }

        RemoveUserConnection(connection.UserId.Value, connectionId);
        return EvaluatePresenceTransition(connection.UserId.Value, DateTime.UtcNow);
    }

    public PresenceTransition Subscribe(Guid connectionId, Guid userId)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
        {
            return new PresenceTransition(false, null, false, false, 0);
        }

        if (connection.UserId.HasValue)
        {
            RemoveUserConnection(connection.UserId.Value, connectionId);
        }

        connection.UserId = userId;
        connection.IsAppForeground = true;
        connection.LastHeartbeatAtUtc = DateTime.UtcNow;
        var set = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, byte>());
        set[connectionId] = 0;
        return EvaluatePresenceTransition(userId, DateTime.UtcNow);
    }

    public PresenceTransition MarkHeartbeat(Guid connectionId, bool isAppForeground, DateTime utcNow)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
        {
            return new PresenceTransition(false, null, false, false, 0);
        }

        connection.LastHeartbeatAtUtc = utcNow;
        connection.IsAppForeground = isAppForeground;
        if (!connection.UserId.HasValue)
        {
            return new PresenceTransition(true, null, false, false, 0);
        }

        return EvaluatePresenceTransition(connection.UserId.Value, utcNow);
    }

    public IReadOnlyList<PresenceTransition> SweepStaleConnections(DateTime utcNow, TimeSpan timeout)
    {
        var staleConnectionIds = _connections.Values
            .Where(x => utcNow - x.LastHeartbeatAtUtc > timeout)
            .Select(x => x.ConnectionId)
            .ToList();

        if (staleConnectionIds.Count == 0)
        {
            return Array.Empty<PresenceTransition>();
        }

        var transitions = new List<PresenceTransition>(staleConnectionIds.Count);
        foreach (var id in staleConnectionIds)
        {
            var transition = Unregister(id);
            if (transition.Success && transition.UserId.HasValue)
            {
                transitions.Add(transition);
            }
        }

        return transitions;
    }

    public IReadOnlyList<Guid> GetConnectionIds()
    {
        return _connections.Keys.ToList();
    }

    public async Task<bool> PushToConnectionAsync(Guid connectionId, ServerMessage message, CancellationToken cancellationToken)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
        {
            return false;
        }

        try
        {
            await connection.SendAsync(message, cancellationToken);
            return true;
        }
        catch
        {
            Unregister(connectionId);
            return false;
        }
    }

    public bool IsUserOnline(Guid userId)
    {
        return _onlineState.TryGetValue(userId, out var isOnline) && isOnline;
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

    private PresenceTransition EvaluatePresenceTransition(Guid userId, DateTime utcNow)
    {
        var currentOnline = ComputeUserOnline(userId, utcNow);
        var previousOnline = _onlineState.TryGetValue(userId, out var stored) && stored;

        _onlineState[userId] = currentOnline;

        var changed = currentOnline != previousOnline;
        var lastSeenAt = currentOnline
            ? 0L
            : new DateTimeOffset(utcNow).ToUnixTimeMilliseconds();

        return new PresenceTransition(true, userId, changed, currentOnline, lastSeenAt);
    }

    private bool ComputeUserOnline(Guid userId, DateTime utcNow)
    {
        if (!_userConnections.TryGetValue(userId, out var connections) || connections.IsEmpty)
        {
            return false;
        }

        foreach (var connectionId in connections.Keys)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
            {
                continue;
            }

            if (!connection.IsAppForeground)
            {
                continue;
            }

            if (utcNow - connection.LastHeartbeatAtUtc <= OnlineTtl)
            {
                return true;
            }
        }

        return false;
    }
}
