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
        try
        {
            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA busy_timeout=30000;
                PRAGMA synchronous=NORMAL;
                """;
            cmd.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Best-effort: pragmas may fail on read-only or not-yet-initialized databases.
            // DatabaseSeeder.Migrate() applies WAL mode explicitly after schema setup.
        }
    }
}
