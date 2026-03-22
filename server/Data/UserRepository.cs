using Dapper;
using Npgsql;

namespace Mingle.Server.Data;

public interface IUserRepository
{
    Task<UserRecord> UpsertByAccountKeyAsync(string accountKey);
    Task<UserRecord?> GetByAccountKeyAsync(string accountKey);
    Task<UserRecord?> GetByUserIdAsync(Guid userId);
    Task<UserRecord?> UpdateProfileAsync(Guid userId, string displayName, string username);
    Task TouchLastSeenAsync(Guid userId);
    Task<IReadOnlyList<UserRecord>> SearchByUsernameAsync(Guid requesterUserId, string query, int limit);
}

public sealed class UserRepository(NpgsqlDataSource dataSource) : IUserRepository
{
    public async Task<UserRecord> UpsertByAccountKeyAsync(string accountKey)
    {
        var fallbackUsername = $"user_{accountKey[..8]}";
        const string sql = """
            INSERT INTO users (account_key, display_name, username, last_seen_at)
            VALUES (@AccountKey, 'WallDev', @FallbackUsername, NOW())
            ON CONFLICT (account_key)
            DO UPDATE SET account_key = EXCLUDED.account_key
            RETURNING id,
                      account_key AS AccountKey,
                      display_name AS DisplayName,
                      username AS Username,
                      created_at AS CreatedAt,
                      last_seen_at AS LastSeenAt;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleAsync<UserRecord>(sql, new { AccountKey = accountKey, FallbackUsername = fallbackUsername });
    }

    public async Task<UserRecord?> GetByAccountKeyAsync(string accountKey)
    {
        const string sql = """
            SELECT id,
                   account_key AS AccountKey,
                   display_name AS DisplayName,
                   username AS Username,
                   created_at AS CreatedAt,
                   last_seen_at AS LastSeenAt
            FROM users
            WHERE account_key = @AccountKey;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<UserRecord>(sql, new { AccountKey = accountKey });
    }

    public async Task<UserRecord?> GetByUserIdAsync(Guid userId)
    {
        const string sql = """
            SELECT id,
                   account_key AS AccountKey,
                   display_name AS DisplayName,
                   username AS Username,
                   created_at AS CreatedAt,
                   last_seen_at AS LastSeenAt
            FROM users
            WHERE id = @UserId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<UserRecord>(sql, new { UserId = userId });
    }

    public async Task<UserRecord?> UpdateProfileAsync(Guid userId, string displayName, string username)
    {
        const string sql = """
            UPDATE users
            SET display_name = @DisplayName,
                username = @Username
            WHERE id = @UserId
            RETURNING id,
                      account_key AS AccountKey,
                      display_name AS DisplayName,
                      username AS Username,
                      created_at AS CreatedAt,
                      last_seen_at AS LastSeenAt;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();

        try
        {
            return await connection.QuerySingleOrDefaultAsync<UserRecord>(sql, new
            {
                UserId = userId,
                DisplayName = displayName,
                Username = username
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "users_username_ci_uidx")
        {
            return null;
        }
    }

    public async Task TouchLastSeenAsync(Guid userId)
    {
        const string sql = """
            UPDATE users SET last_seen_at = NOW()
            WHERE id = @UserId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task<IReadOnlyList<UserRecord>> SearchByUsernameAsync(Guid requesterUserId, string query, int limit)
    {
        const string sql = """
            SELECT id,
                   account_key AS AccountKey,
                   display_name AS DisplayName,
                   username AS Username,
                   created_at AS CreatedAt,
                   last_seen_at AS LastSeenAt
            FROM users
            WHERE id <> @RequesterUserId
              AND username ILIKE @Pattern
            ORDER BY username ASC
            LIMIT @Limit;
            """;

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return Array.Empty<UserRecord>();
        }

        await using var connection = await dataSource.OpenConnectionAsync();
        var rows = await connection.QueryAsync<UserRecord>(sql, new
        {
            RequesterUserId = requesterUserId,
            Pattern = $"%{normalizedQuery}%",
            Limit = Math.Clamp(limit, 1, 50)
        });

        return rows.ToList();
    }
}
