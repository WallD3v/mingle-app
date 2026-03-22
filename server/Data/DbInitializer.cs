using Dapper;
using Npgsql;

namespace Mingle.Server.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(NpgsqlDataSource dataSource)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                account_key CHAR(64) NOT NULL UNIQUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        await connection.ExecuteAsync(sql);
    }
}
