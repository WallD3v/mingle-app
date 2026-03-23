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

        const string dialogsSql = """
            CREATE TABLE IF NOT EXISTS dialogs (
                id UUID PRIMARY KEY,
                user_a_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                user_b_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                last_message_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                CHECK (user_a_id <> user_b_id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS dialogs_user_pair_uidx
            ON dialogs ((LEAST(user_a_id, user_b_id)), (GREATEST(user_a_id, user_b_id)));

            CREATE INDEX IF NOT EXISTS dialogs_user_a_idx ON dialogs (user_a_id, last_message_at DESC);
            CREATE INDEX IF NOT EXISTS dialogs_user_b_idx ON dialogs (user_b_id, last_message_at DESC);

            CREATE TABLE IF NOT EXISTS messages (
                id UUID PRIMARY KEY,
                dialog_id UUID NOT NULL REFERENCES dialogs(id) ON DELETE CASCADE,
                sender_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                body TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                read_by_recipient_at TIMESTAMPTZ
            );

            ALTER TABLE messages ADD COLUMN IF NOT EXISTS read_by_recipient_at TIMESTAMPTZ;

            CREATE INDEX IF NOT EXISTS messages_dialog_created_idx
            ON messages (dialog_id, created_at ASC);
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        await connection.ExecuteAsync(createTableSql);
        await connection.ExecuteAsync(migrationSql);
        await connection.ExecuteAsync(dialogsSql);
    }
}
