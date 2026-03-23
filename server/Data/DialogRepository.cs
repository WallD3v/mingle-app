using Dapper;
using Npgsql;

namespace Mingle.Server.Data;

public interface IDialogRepository
{
    Task<IReadOnlyList<DialogListItemRecord>> GetDialogsAsync(Guid userId, int limit);
    Task<DialogThreadRecord?> GetDialogAsync(Guid userId, Guid peerUserId, int limit, long? beforeUnixMs = null);
    Task<(DialogMessageRecord Message, DialogThreadRecord Thread)?> SendMessageAsync(Guid senderUserId, Guid peerUserId, string text, int historyLimit);
}

public sealed class DialogRepository(NpgsqlDataSource dataSource) : IDialogRepository
{
    public async Task<IReadOnlyList<DialogListItemRecord>> GetDialogsAsync(Guid userId, int limit)
    {
        const string sql = """
            SELECT
                d.id AS DialogId,
                CASE WHEN d.user_a_id = @UserId THEN u_b.id ELSE u_a.id END AS PeerUserId,
                CASE WHEN d.user_a_id = @UserId THEN u_b.display_name ELSE u_a.display_name END AS PeerDisplayName,
                CASE WHEN d.user_a_id = @UserId THEN u_b.username ELSE u_a.username END AS PeerUsername,
                CASE WHEN d.user_a_id = @UserId THEN u_b.last_seen_at ELSE u_a.last_seen_at END AS PeerLastSeenAt,
                COALESCE(m.body, '') AS LastMessageText,
                d.last_message_at AS LastMessageAt,
                COALESCE(unread.unread_count, 0) AS UnreadCount
            FROM dialogs d
            JOIN users u_a ON u_a.id = d.user_a_id
            JOIN users u_b ON u_b.id = d.user_b_id
            LEFT JOIN LATERAL (
                SELECT body
                FROM messages
                WHERE dialog_id = d.id
                ORDER BY created_at DESC
                LIMIT 1
            ) m ON TRUE
            LEFT JOIN LATERAL (
                SELECT COUNT(*)::int AS unread_count
                FROM messages um
                WHERE um.dialog_id = d.id
                  AND um.sender_user_id <> @UserId
                  AND um.read_by_recipient_at IS NULL
            ) unread ON TRUE
            WHERE d.user_a_id = @UserId OR d.user_b_id = @UserId
            ORDER BY d.last_message_at DESC
            LIMIT @Limit;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        var rows = await connection.QueryAsync<DialogListItemRecord>(sql, new
        {
            UserId = userId,
            Limit = Math.Clamp(limit, 1, 100)
        });
        return rows.ToList();
    }

    public async Task<DialogThreadRecord?> GetDialogAsync(Guid userId, Guid peerUserId, int limit, long? beforeUnixMs = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await GetDialogInternalAsync(connection, userId, peerUserId, limit, beforeUnixMs, null);
    }

    public async Task<(DialogMessageRecord Message, DialogThreadRecord Thread)?> SendMessageAsync(Guid senderUserId, Guid peerUserId, string text, int historyLimit)
    {
        var normalizedText = text.Trim();
        if (normalizedText.Length == 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var peer = await GetPeerAsync(connection, peerUserId, transaction);
        if (peer is null)
        {
            await transaction.RollbackAsync();
            return null;
        }

        const string upsertDialogSql = """
            INSERT INTO dialogs (id, user_a_id, user_b_id, created_at, last_message_at)
            VALUES (@DialogId, @UserAId, @UserBId, NOW(), NOW())
            ON CONFLICT ((LEAST(user_a_id, user_b_id)), (GREATEST(user_a_id, user_b_id)))
            DO UPDATE SET last_message_at = NOW()
            RETURNING id;
            """;

        var generatedDialogId = Guid.NewGuid();
        var dialogId = await connection.ExecuteScalarAsync<Guid>(upsertDialogSql, new
        {
            DialogId = generatedDialogId,
            UserAId = senderUserId,
            UserBId = peerUserId
        }, transaction);

        const string insertMessageSql = """
            INSERT INTO messages (id, dialog_id, sender_user_id, body, created_at)
            VALUES (@MessageId, @DialogId, @SenderUserId, @Body, NOW())
            RETURNING id AS MessageId,
                      dialog_id AS DialogId,
                      sender_user_id AS SenderUserId,
                      body AS Text,
                      created_at AS CreatedAt;
            """;

        var message = await connection.QuerySingleAsync<DialogMessageRecord>(insertMessageSql, new
        {
            MessageId = Guid.NewGuid(),
            DialogId = dialogId,
            SenderUserId = senderUserId,
            Body = normalizedText
        }, transaction);

        await transaction.CommitAsync();

        var thread = await GetDialogInternalAsync(connection, senderUserId, peerUserId, historyLimit, null, null);
        if (thread is null)
        {
            return null;
        }

        return (message, thread);
    }

    private static async Task<DialogThreadRecord?> GetDialogInternalAsync(
        NpgsqlConnection connection,
        Guid userId,
        Guid peerUserId,
        int limit,
        long? beforeUnixMs,
        NpgsqlTransaction? transaction)
    {
        var peer = await GetPeerAsync(connection, peerUserId, transaction);
        if (peer is null)
        {
            return null;
        }

        const string dialogSql = """
            SELECT id
            FROM dialogs
            WHERE (user_a_id = @UserId AND user_b_id = @PeerUserId)
               OR (user_a_id = @PeerUserId AND user_b_id = @UserId)
            LIMIT 1;
            """;

        var dialogId = await connection.ExecuteScalarAsync<Guid?>(dialogSql, new
        {
            UserId = userId,
            PeerUserId = peerUserId
        }, transaction);

        if (!dialogId.HasValue)
        {
            return new DialogThreadRecord
            {
                DialogId = null,
                PeerUserId = peer.PeerUserId,
                PeerDisplayName = peer.PeerDisplayName,
                PeerUsername = peer.PeerUsername,
                PeerLastSeenAt = peer.PeerLastSeenAt,
                Messages = Array.Empty<DialogMessageRecord>(),
                HasMoreBefore = false,
                OldestLoadedUnixMs = 0
            };
        }

        const string markReadSql = """
            UPDATE messages
            SET read_by_recipient_at = NOW()
            WHERE dialog_id = @DialogId
              AND sender_user_id <> @UserId
              AND read_by_recipient_at IS NULL;
            """;
        await connection.ExecuteAsync(markReadSql, new { DialogId = dialogId.Value, UserId = userId }, transaction);

        const string messagesSql = """
            SELECT id AS MessageId,
                   dialog_id AS DialogId,
                   sender_user_id AS SenderUserId,
                   body AS Text,
                   created_at AS CreatedAt
            FROM messages
            WHERE dialog_id = @DialogId
              AND (@BeforeUnixMs IS NULL OR created_at < to_timestamp(@BeforeUnixMs / 1000.0))
            ORDER BY created_at DESC
            LIMIT @FetchLimit;
            """;

        var effectiveLimit = Math.Clamp(limit, 1, 200);
        var fetched = (await connection.QueryAsync<DialogMessageRecord>(messagesSql, new
        {
            DialogId = dialogId.Value,
            BeforeUnixMs = beforeUnixMs,
            FetchLimit = effectiveLimit + 1
        }, transaction)).ToList();

        var hasMoreBefore = fetched.Count > effectiveLimit;
        if (hasMoreBefore)
        {
            fetched.RemoveAt(fetched.Count - 1);
        }

        fetched.Reverse();
        var oldestLoadedUnixMs = fetched.Count == 0
            ? 0
            : new DateTimeOffset(fetched[0].CreatedAt).ToUnixTimeMilliseconds();

        return new DialogThreadRecord
        {
            DialogId = dialogId.Value,
            PeerUserId = peer.PeerUserId,
            PeerDisplayName = peer.PeerDisplayName,
            PeerUsername = peer.PeerUsername,
            PeerLastSeenAt = peer.PeerLastSeenAt,
            Messages = fetched,
            HasMoreBefore = hasMoreBefore,
            OldestLoadedUnixMs = oldestLoadedUnixMs
        };
    }

    private static Task<PeerProjection?> GetPeerAsync(NpgsqlConnection connection, Guid peerUserId, NpgsqlTransaction? transaction)
    {
        const string peerSql = """
            SELECT id AS PeerUserId,
                   display_name AS PeerDisplayName,
                   username AS PeerUsername,
                   last_seen_at AS PeerLastSeenAt
            FROM users
            WHERE id = @PeerUserId
            LIMIT 1;
            """;

        return connection.QuerySingleOrDefaultAsync<PeerProjection>(peerSql, new { PeerUserId = peerUserId }, transaction);
    }

    private sealed class PeerProjection
    {
        public Guid PeerUserId { get; set; }
        public string PeerDisplayName { get; set; } = string.Empty;
        public string PeerUsername { get; set; } = string.Empty;
        public DateTime PeerLastSeenAt { get; set; }
    }
}
