using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

/// <summary>
/// Creates a timestamped copy of each SQLite database file before EF migrations
/// are applied. Only backs up when there are pending migrations — clean boots
/// (up-to-date schema) are skipped. Backup failure is never fatal: a prominent
/// warning is logged and the caller continues with the migration.
///
/// Defaults: backups land in <c>&lt;DataRoot&gt;/backups/</c>, last 5 copies per
/// database are kept. Both values are configurable via
/// <see cref="BackupRoot"/> and <see cref="RetainCount"/>.
/// </summary>
public static class DatabaseBackupService
{
    /// <summary>Directory under which timestamped backup files are written.</summary>
    public static string BackupRoot { get; set; } = Path.Combine(AppFiles.DataPath, "backups");

    /// <summary>Number of backup files to retain per database. Oldest are pruned first.</summary>
    public static int RetainCount { get; set; } = 5;

    /// <summary>
    /// Backs up <paramref name="dbPath"/> when <paramref name="pendingMigrationCount"/> is
    /// greater than zero. The backup file is named
    /// <c>&lt;dbname&gt;.&lt;yyyyMMddHHmmss&gt;.db</c>.
    ///
    /// Returns <c>true</c> when a backup was written, <c>false</c> when skipped (no pending
    /// migrations) or when the backup attempt failed (failure is non-fatal — caller proceeds).
    /// </summary>
    public static bool BackupBeforeMigration(string dbPath, int pendingMigrationCount)
    {
        if (pendingMigrationCount == 0)
            return false;

        if (!File.Exists(dbPath))
            return false;

        try
        {
            Directory.CreateDirectory(BackupRoot);

            string dbName = Path.GetFileNameWithoutExtension(dbPath);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string backupFileName = $"{dbName}.{timestamp}.db";
            string backupPath = Path.Combine(BackupRoot, backupFileName);

            File.Copy(dbPath, backupPath, overwrite: false);

            Logger.Setup(
                $"Database backup created: {backupFileName} ({pendingMigrationCount} pending migration(s))"
            );

            PruneOldBackups(dbName);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"WARNING: Could not back up database '{Path.GetFileName(dbPath)}' before migration — {ex.Message}. "
                    + "Continuing with migration. If this upgrade is destructive, restore from a manual backup.",
                LogEventLevel.Warning
            );
            return false;
        }
    }

    /// <summary>
    /// Deletes the oldest backup files for <paramref name="dbName"/> until at most
    /// <see cref="RetainCount"/> remain.
    /// </summary>
    private static void PruneOldBackups(string dbName)
    {
        try
        {
            string[] existing = Directory
                .GetFiles(BackupRoot, $"{dbName}.*.db")
                .OrderBy(filePath => filePath)
                .ToArray();

            int toDelete = existing.Length - RetainCount;
            for (int idx = 0; idx < toDelete; idx++)
            {
                File.Delete(existing[idx]);
                Logger.Setup(
                    $"Pruned old backup: {Path.GetFileName(existing[idx])}",
                    LogEventLevel.Verbose
                );
            }
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"WARNING: Backup pruning failed for '{dbName}': {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }
}
