// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Data.Sqlite;
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
    public static string BackupRoot { get; set; } = Path.Combine(path1: AppFiles.DataPath, path2: "backups");

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

        return BackupNow(dbPath: dbPath, reason: $"{pendingMigrationCount} pending migration(s)");
    }

    /// <summary>
    /// Backs up <paramref name="dbPath"/> unconditionally — used by the periodic
    /// <see cref="NoMercy.Queue.MediaServer.Jobs.DatabaseBackupCronJob"/> so a recent
    /// recovery point exists even on installs that go a long time between migrations.
    /// <paramref name="reason"/> is cosmetic, folded into the log line only.
    ///
    /// Returns <c>true</c> when a backup was written, <c>false</c> when the source file
    /// doesn't exist or the backup attempt failed (failure is non-fatal — caller proceeds).
    /// </summary>
    public static bool BackupNow(string dbPath, string reason)
    {
        if (!File.Exists(path: dbPath))
            return false;

        try
        {
            Directory.CreateDirectory(path: BackupRoot);

            string dbName = Path.GetFileNameWithoutExtension(path: dbPath);
            string timestamp = DateTime.UtcNow.ToString(format: "yyyyMMddHHmmss");
            string backupFileName = $"{dbName}.{timestamp}.db";
            string backupPath = Path.Combine(path1: BackupRoot, path2: backupFileName);

            if (File.Exists(path: backupPath))
                throw new IOException(message: $"Backup file already exists: {backupPath}");

            // SQLite's online-backup API (not File.Copy) so a database running in
            // WAL mode is captured consistently — a plain file copy only sees the
            // main .db file and silently misses committed-but-not-yet-checkpointed
            // rows sitting in the -wal file.
            //
            // Pooling=False on both ends: these connections are opened once for a
            // single backup and immediately disposed. With the default pooled
            // behaviour, Microsoft.Data.Sqlite keeps the native handle (and the
            // underlying OS file handle) alive in the pool after Dispose() so it
            // can be reused by a future connection with the same connection
            // string — but the backup path is unique per timestamp, so that
            // handle is never reused. Left pooled, every backup ever taken over
            // the process's lifetime would leak an open handle on the backup
            // file, keeping it locked against deletion/pruning/manual access on
            // Windows.
            using (SqliteConnection source = new(connectionString: $"Data Source={dbPath}; Pooling=False;"))
            using (SqliteConnection destination = new(connectionString: $"Data Source={backupPath}; Pooling=False;"))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination: destination);
            }

            Logger.Setup(message: $"Database backup created: {backupFileName} ({reason})");

            PruneOldBackups(dbName: dbName);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"WARNING: Could not back up database '{Path.GetFileName(path: dbPath)}' — {ex.Message}. "
                         + "If this coincides with an upgrade, restore from a manual backup.",
                level: LogEventLevel.Warning
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
                .GetFiles(path: BackupRoot, searchPattern: $"{dbName}.*.db")
                .OrderBy(keySelector: filePath => filePath)
                .ToArray();

            int toDelete = existing.Length - RetainCount;
            for (int idx = 0; idx < toDelete; idx++)
            {
                File.Delete(path: existing[idx]);
                Logger.Setup(
                    message: $"Pruned old backup: {Path.GetFileName(path: existing[idx])}",
                    level: LogEventLevel.Verbose
                );
            }
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"WARNING: Backup pruning failed for '{dbName}': {ex.Message}",
                level: LogEventLevel.Warning
            );
        }
    }
}
