using Dapper;
using Npgsql;

namespace Mingle.Server.Data;

public interface IUserRepository
{
    Task<UserRecord> UpsertByAccountKeyAsync(string accountKey);
    Task<UserRecord?> GetByAccountKeyAsync(string accountKey);
}

public sealed class UserRepository(NpgsqlDataSource dataSource) : IUserRepository
{
    public async Task<UserRecord> UpsertByAccountKeyAsync(string accountKey)
    {
        const string sql = """
            INSERT INTO users (account_key)
            VALUES (@AccountKey)
            ON CONFLICT (account_key)
            DO UPDATE SET account_key = EXCLUDED.account_key
            RETURNING id, account_key AS AccountKey, created_at AS CreatedAt;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleAsync<UserRecord>(sql, new { AccountKey = accountKey });
    }

    public async Task<UserRecord?> GetByAccountKeyAsync(string accountKey)
    {
        const string sql = """
            SELECT id, account_key AS AccountKey, created_at AS CreatedAt
            FROM users
            WHERE account_key = @AccountKey;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<UserRecord>(sql, new { AccountKey = accountKey });
    }
}
