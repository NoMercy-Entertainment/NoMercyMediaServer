using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NoMercyQueue.Sqlite;

internal class SqliteQueueConnectionInterceptor : DbConnectionInterceptor
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
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=30000;
            PRAGMA synchronous=NORMAL;
            """;
        cmd.ExecuteNonQuery();
    }
}
