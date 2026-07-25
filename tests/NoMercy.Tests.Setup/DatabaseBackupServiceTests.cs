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
using NoMercy.Service.Seeds;

namespace NoMercy.Tests.Setup;

[Trait("Category", "Unit")]
public sealed class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _backupDir;
    private readonly string _originalBackupRoot;
    private readonly int _originalRetainCount;

    public DatabaseBackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nm_backup_test_{Guid.NewGuid():N}");
        _backupDir = Path.Combine(_tempDir, "backups");
        Directory.CreateDirectory(_tempDir);

        _originalBackupRoot = DatabaseBackupService.BackupRoot;
        _originalRetainCount = DatabaseBackupService.RetainCount;

        DatabaseBackupService.BackupRoot = _backupDir;
    }

    public void Dispose()
    {
        DatabaseBackupService.BackupRoot = _originalBackupRoot;
        DatabaseBackupService.RetainCount = _originalRetainCount;

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // A real SQLite database, not a text file: the service copies through
    // SQLite's online-backup API, which rejects anything without a valid
    // database header.
    private string CreateFakeDb(string name = "media.db")
    {
        string path = Path.Combine(_tempDir, name);
        using SqliteConnection connection = new($"Data Source={path}; Pooling=False;");
        connection.Open();
        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText =
            "CREATE TABLE marker (content TEXT); INSERT INTO marker (content) VALUES ('fake sqlite content');";
        seed.ExecuteNonQuery();
        return path;
    }

    [Fact]
    public void BackupBeforeMigration_NoPendingMigrations_SkipsBackup()
    {
        string dbPath = CreateFakeDb();

        bool result = DatabaseBackupService.BackupBeforeMigration(dbPath, pendingMigrationCount: 0);

        Assert.False(result);
        Assert.False(Directory.Exists(_backupDir));
    }

    [Fact]
    public void BackupBeforeMigration_DbDoesNotExist_SkipsBackup()
    {
        string nonExistentPath = Path.Combine(_tempDir, "nonexistent.db");

        bool result = DatabaseBackupService.BackupBeforeMigration(
            nonExistentPath,
            pendingMigrationCount: 3
        );

        Assert.False(result);
        Assert.False(Directory.Exists(_backupDir));
    }

    [Fact]
    public void BackupBeforeMigration_PendingMigrations_CreatesBackupFile()
    {
        string dbPath = CreateFakeDb("media.db");

        bool result = DatabaseBackupService.BackupBeforeMigration(dbPath, pendingMigrationCount: 2);

        Assert.True(result);
        Assert.True(Directory.Exists(_backupDir));

        string[] backups = Directory.GetFiles(_backupDir, "media.*.db");
        Assert.Single(backups);
    }

    [Fact]
    public void BackupBeforeMigration_BackupContentMatchesSource()
    {
        string dbPath = CreateFakeDb("media.db");

        DatabaseBackupService.BackupBeforeMigration(dbPath, pendingMigrationCount: 1);

        string[] backups = Directory.GetFiles(_backupDir, "media.*.db");
        using SqliteConnection backup = new($"Data Source={backups[0]}; Pooling=False;");
        backup.Open();
        using SqliteCommand query = backup.CreateCommand();
        query.CommandText = "SELECT content FROM marker;";
        Assert.Equal("fake sqlite content", (string?)query.ExecuteScalar());
    }

    [Fact]
    public void BackupBeforeMigration_BackupFileNameContainsTimestamp()
    {
        string dbPath = CreateFakeDb("queue.db");

        DatabaseBackupService.BackupBeforeMigration(dbPath, pendingMigrationCount: 1);

        string[] backups = Directory.GetFiles(_backupDir, "queue.*.db");
        string fileName = Path.GetFileName(backups[0]);

        // Pattern: queue.<yyyyMMddHHmmss>.db — 14-digit timestamp between dots
        string[] parts = fileName.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.Equal("queue", parts[0]);
        Assert.Equal("db", parts[2]);
        Assert.Equal(14, parts[1].Length);
        Assert.True(long.TryParse(parts[1], out _), "Timestamp portion must be numeric");
    }

    [Fact]
    public void BackupBeforeMigration_ExceedsRetainCount_PrunesOldest()
    {
        DatabaseBackupService.RetainCount = 3;
        string dbPath = CreateFakeDb("app.db");

        // Seed four existing backup files with ascending timestamps
        Directory.CreateDirectory(_backupDir);
        for (int index = 1; index <= 4; index++)
        {
            string fake = Path.Combine(_backupDir, $"app.2026010100000{index}.db");
            File.WriteAllText(fake, "old backup");
        }

        DatabaseBackupService.BackupBeforeMigration(dbPath, pendingMigrationCount: 1);

        string[] remaining = Directory.GetFiles(_backupDir, "app.*.db");
        Assert.Equal(3, remaining.Length);

        // The oldest (lowest timestamp) must have been deleted
        string[] fileNames = remaining.Select(Path.GetFileName).ToArray()!;
        Assert.DoesNotContain("app.20260101000001.db", fileNames);
    }

    [Fact]
    public void BackupBeforeMigration_BelowRetainCount_KeepsAll()
    {
        DatabaseBackupService.RetainCount = 5;
        string dbPath = CreateFakeDb("media.db");

        // Seed two existing backup files
        Directory.CreateDirectory(_backupDir);
        File.WriteAllText(Path.Combine(_backupDir, "media.20260101000001.db"), "old");
        File.WriteAllText(Path.Combine(_backupDir, "media.20260101000002.db"), "old");

        DatabaseBackupService.BackupBeforeMigration(dbPath, pendingMigrationCount: 1);

        string[] remaining = Directory.GetFiles(_backupDir, "media.*.db");
        Assert.Equal(3, remaining.Length);
    }

    [Fact]
    public void BackupBeforeMigration_MultipleDbNames_PrunesIndependently()
    {
        DatabaseBackupService.RetainCount = 2;
        string mediaDb = CreateFakeDb("media.db");
        string queueDb = CreateFakeDb("queue.db");

        Directory.CreateDirectory(_backupDir);
        for (int index = 1; index <= 3; index++)
        {
            File.WriteAllText(Path.Combine(_backupDir, $"media.2026010100000{index}.db"), "old");
            File.WriteAllText(Path.Combine(_backupDir, $"queue.2026010100000{index}.db"), "old");
        }

        DatabaseBackupService.BackupBeforeMigration(mediaDb, pendingMigrationCount: 1);
        DatabaseBackupService.BackupBeforeMigration(queueDb, pendingMigrationCount: 1);

        string[] mediaBackups = Directory.GetFiles(_backupDir, "media.*.db");
        string[] queueBackups = Directory.GetFiles(_backupDir, "queue.*.db");

        Assert.Equal(2, mediaBackups.Length);
        Assert.Equal(2, queueBackups.Length);
    }

    [Fact]
    public void BackupBeforeMigration_WalResidentUncheckpointedRow_IsIncludedInBackup()
    {
        string dbPath = Path.Combine(_tempDir, "wal-media.db");
        string[] backups;

        using (SqliteConnection walConnection = new($"Data Source={dbPath}"))
        {
            walConnection.Open();
            using (SqliteCommand walMode = walConnection.CreateCommand())
            {
                walMode.CommandText = "PRAGMA journal_mode=WAL;";
                walMode.ExecuteNonQuery();
            }
            using (SqliteCommand createTable = walConnection.CreateCommand())
            {
                createTable.CommandText =
                    "CREATE TABLE Probe (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);";
                createTable.ExecuteNonQuery();
            }
            using (SqliteCommand insert = walConnection.CreateCommand())
            {
                insert.CommandText = "INSERT INTO Probe (Value) VALUES ('wal-resident-row');";
                insert.ExecuteNonQuery();
            }

            Assert.True(File.Exists(dbPath + "-wal"));

            bool backedUp = DatabaseBackupService.BackupBeforeMigration(
                dbPath,
                pendingMigrationCount: 1
            );

            Assert.True(backedUp);
            backups = Directory.GetFiles(_backupDir, "wal-media.*.db");
            Assert.Single(backups);
        }

        SqliteConnection.ClearAllPools();

        object? value;
        using (SqliteConnection verifyConnection = new($"Data Source={backups[0]}"))
        {
            verifyConnection.Open();
            using SqliteCommand select = verifyConnection.CreateCommand();
            select.CommandText = "SELECT Value FROM Probe;";
            value = select.ExecuteScalar();
        }

        SqliteConnection.ClearAllPools();

        Assert.Equal("wal-resident-row", value);
    }

    [Fact]
    public void BackupBeforeMigration_InvalidBackupRoot_ReturnsFalseWithoutThrowing()
    {
        DatabaseBackupService.BackupRoot = "\0invalid\0path";
        string dbPath = CreateFakeDb("media.db");

        bool result = DatabaseBackupService.BackupBeforeMigration(dbPath, pendingMigrationCount: 1);

        // Must not throw — failure is non-fatal
        Assert.False(result);
    }

    // ── BackupNow (unconditional — used by the daily backup cron job) ────────

    [Fact]
    public void BackupNow_DbExists_CreatesBackupFileRegardlessOfMigrations()
    {
        string dbPath = CreateFakeDb("media.db");

        bool result = DatabaseBackupService.BackupNow(dbPath, "daily scheduled backup");

        Assert.True(result);
        string[] backups = Directory.GetFiles(_backupDir, "media.*.db");
        Assert.Single(backups);
    }

    [Fact]
    public void BackupNow_DbDoesNotExist_ReturnsFalseWithoutThrowing()
    {
        string nonExistentPath = Path.Combine(_tempDir, "nonexistent.db");

        bool result = DatabaseBackupService.BackupNow(nonExistentPath, "daily scheduled backup");

        Assert.False(result);
        Assert.False(Directory.Exists(_backupDir));
    }

    [Fact]
    public void BackupNow_RespectsRetainCountAcrossRepeatedCalls()
    {
        DatabaseBackupService.RetainCount = 2;
        string dbPath = CreateFakeDb("media.db");

        Directory.CreateDirectory(_backupDir);
        for (int index = 1; index <= 3; index++)
            File.WriteAllText(
                Path.Combine(_backupDir, $"media.2026010100000{index}.db"),
                "old backup"
            );

        DatabaseBackupService.BackupNow(dbPath, "daily scheduled backup");

        string[] remaining = Directory.GetFiles(_backupDir, "media.*.db");
        Assert.Equal(2, remaining.Length);
    }
}
