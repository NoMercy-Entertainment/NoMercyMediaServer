using System.Data;
using System.Data.Common;

namespace NoMercy.Database.Maintenance;

/// <summary>
/// Removes foreign-key-orphaned rows before EF migrations run. EF rebuilds a
/// SQLite table to change a foreign key (SQLite has no ALTER for that), copying
/// rows with enforcement on. A row whose parent was deleted before cascade rules
/// existed then fails the copy with "FOREIGN KEY constraint failed" and bricks
/// the migration. Deleting those orphans first is exactly what the cascade rule
/// would have done, so the migration applies. No-op on a clean database.
/// </summary>
public static class ForeignKeyOrphanCleaner
{
    private const int MaxPasses = 25;
    private const int DeleteBatchSize = 500;

    /// <summary>
    /// Deletes every row reported by <c>PRAGMA foreign_key_check</c>, repeating
    /// until none remain (deleting a parent-orphan can expose its own children).
    /// Returns the number of rows removed per table.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Clean(
        DbConnection connection,
        string contextName
    )
    {
        _ = contextName;

        if (connection.State != ConnectionState.Open)
            connection.Open();

        Dictionary<string, int> deletedPerTable = new();
        bool foreignKeysWereOn = ReadForeignKeysEnabled(connection);

        try
        {
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                List<KeyValuePair<string, long>> orphans = FindOrphans(connection);
                if (orphans.Count == 0)
                    break;

                // Disable enforcement so deleting a parent-orphan doesn't trip its
                // own cascade mid-clean; the next pass re-checks what that exposed.
                SetForeignKeys(connection, false);

                foreach (
                    IGrouping<string, KeyValuePair<string, long>> table in orphans.GroupBy(orphan =>
                        orphan.Key
                    )
                )
                foreach (KeyValuePair<string, long>[] batch in table.Chunk(DeleteBatchSize))
                {
                    string rowIds = string.Join(",", batch.Select(orphan => orphan.Value));
                    int removed = Execute(
                        connection,
                        $"DELETE FROM \"{table.Key}\" WHERE rowid IN ({rowIds});"
                    );
                    deletedPerTable[table.Key] =
                        deletedPerTable.GetValueOrDefault(table.Key) + removed;
                }
            }
        }
        finally
        {
            SetForeignKeys(connection, foreignKeysWereOn);
        }

        return deletedPerTable;
    }

    /// <summary>
    /// Human-readable list of the current foreign-key violations, for logging
    /// when a migration still fails on a constraint.
    /// </summary>
    public static IReadOnlyList<string> DescribeViolations(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        List<string> descriptions = new();
        using DbCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        using DbDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string child = reader.GetString(0);
            string parent = reader.IsDBNull(2) ? "?" : reader.GetString(2);
            string rowId = reader.IsDBNull(1) ? "?" : reader.GetInt64(1).ToString();
            descriptions.Add($"{child} rowid={rowId} -> {parent}");
        }
        return descriptions;
    }

    private static List<KeyValuePair<string, long>> FindOrphans(DbConnection connection)
    {
        List<KeyValuePair<string, long>> orphans = new();
        using DbCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        using DbDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            // WITHOUT ROWID tables report a null rowid and can't be targeted here.
            if (reader.IsDBNull(1))
                continue;
            orphans.Add(new(reader.GetString(0), reader.GetInt64(1)));
        }
        return orphans;
    }

    private static bool ReadForeignKeysEnabled(DbConnection connection)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        return Convert.ToInt64(command.ExecuteScalar() ?? 0L) == 1;
    }

    private static void SetForeignKeys(DbConnection connection, bool enabled) =>
        Execute(connection, $"PRAGMA foreign_keys = {(enabled ? "ON" : "OFF")};");

    private static int Execute(DbConnection connection, string sql)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }
}
