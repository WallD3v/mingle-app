using Dapper;
using Npgsql;

namespace Mingle.Server.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(NpgsqlDataSource dataSource)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                account_key CHAR(64) NOT NULL UNIQUE,
                display_name TEXT,
                username TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;

        const string migrationSql = """
            ALTER TABLE users ADD COLUMN IF NOT EXISTS display_name TEXT;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS username TEXT;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS last_seen_at TIMESTAMPTZ;

            UPDATE users
            SET display_name = COALESCE(display_name, 'WallDev'),
                username = COALESCE(username, 'user_' || SUBSTRING(account_key, 1, 8)),
                last_seen_at = COALESCE(last_seen_at, NOW());

            ALTER TABLE users ALTER COLUMN display_name SET NOT NULL;
            ALTER TABLE users ALTER COLUMN username SET NOT NULL;
            ALTER TABLE users ALTER COLUMN last_seen_at SET NOT NULL;
            ALTER TABLE users ALTER COLUMN display_name SET DEFAULT 'WallDev';
            ALTER TABLE users ALTER COLUMN last_seen_at SET DEFAULT NOW();

            CREATE UNIQUE INDEX IF NOT EXISTS users_username_ci_uidx ON users (LOWER(username));
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        await connection.ExecuteAsync(createTableSql);
        await connection.ExecuteAsync(migrationSql);
    }
}
