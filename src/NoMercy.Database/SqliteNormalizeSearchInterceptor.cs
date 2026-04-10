using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Database;

public class SqliteNormalizeSearchInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is SqliteConnection sqliteConnection)
        {
            EnableWalMode(sqliteConnection);
            RegisterFunction(sqliteConnection);
        }

        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        if (connection is SqliteConnection sqliteConnection)
        {
            EnableWalMode(sqliteConnection);
            RegisterFunction(sqliteConnection);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void EnableWalMode(SqliteConnection connection)
    {
        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=30000;";
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 8) // SQLITE_READONLY
        {
            // EF Core opens a read-only connection during Exists() checks.
            // WAL mode cannot be set on a read-only connection — skip it silently;
            // the pragma will be applied on the next writable open.
        }
    }

    private static void RegisterFunction(SqliteConnection connection)
    {
        connection.CreateFunction(
            "normalize_search",
            (string? input) => input?.NormalizeSearch().OrEmpty()
        );
    }
}
