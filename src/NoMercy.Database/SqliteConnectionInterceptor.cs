using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NoMercy.Database;

/// <summary>
/// Configures each SQLite connection for concurrent use:
/// - WAL journal mode: allows concurrent readers alongside the writer
/// - busy_timeout: retry for up to 30 s instead of immediately throwing SQLITE_BUSY
/// - synchronous=NORMAL: safe with WAL and faster than FULL
/// </summary>
public class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        ApplyPragmas(connection);
        await Task.CompletedTask;
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using DbCommand cmd = connection.CreateCommand();

        // busy_timeout and synchronous are connection-level — always safe to set.
        cmd.CommandText = """
            PRAGMA busy_timeout=30000;
            PRAGMA synchronous=NORMAL;
            """;
        cmd.ExecuteNonQuery();

        // journal_mode=WAL writes to the database file — may fail on read-only
        // or not-yet-initialized databases. DatabaseSeeder.Migrate() applies it
        // explicitly after schema setup, so this is best-effort.
        try
        {
            using DbCommand walCmd = connection.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Silently skip — WAL will be set after migration completes.
        }
    }
}
