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

[Trait(name: "Category", value: "Unit")]
public sealed class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _backupDir;
    private readonly string _originalBackupRoot;
    private readonly int _originalRetainCount;

    public DatabaseBackupServiceTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"nm_backup_test_{Guid.NewGuid():N}");
        _backupDir = Path.Combine(path1: _tempDir, path2: "backups");
        Directory.CreateDirectory(path: _tempDir);

        _originalBackupRoot = DatabaseBackupService.BackupRoot;
        _originalRetainCount = DatabaseBackupService.RetainCount;

        DatabaseBackupService.BackupRoot = _backupDir;
    }

    public void Dispose()
    {
        DatabaseBackupService.BackupRoot = _originalBackupRoot;
        DatabaseBackupService.RetainCount = _originalRetainCount;

        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    // A real SQLite database, not a text file: the service copies through
    // SQLite's online-backup API, which rejects anything without a valid
    // database header.
    private string CreateFakeDb(string name = "media.db")
    {
        string path = Path.Combine(path1: _tempDir, path2: name);
        using SqliteConnection connection = new(connectionString: $"Data Source={path}; Pooling=False;");
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

        bool result = DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: 0);

        Assert.False(condition: result);
        Assert.False(condition: Directory.Exists(path: _backupDir));
    }

    [Fact]
    public void BackupBeforeMigration_DbDoesNotExist_SkipsBackup()
    {
        string nonExistentPath = Path.Combine(path1: _tempDir, path2: "nonexistent.db");

        bool result = DatabaseBackupService.BackupBeforeMigration(
            dbPath: nonExistentPath,
            pendingMigrationCount: 3
        );

        Assert.False(condition: result);
        Assert.False(condition: Directory.Exists(path: _backupDir));
    }

    [Fact]
    public void BackupBeforeMigration_PendingMigrations_CreatesBackupFile()
    {
        string dbPath = CreateFakeDb(name: "media.db");

        bool result = DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: 2);

        Assert.True(condition: result);
        Assert.True(condition: Directory.Exists(path: _backupDir));

        string[] backups = Directory.GetFiles(path: _backupDir, searchPattern: "media.*.db");
        Assert.Single(collection: backups);
    }

    [Fact]
    public void BackupBeforeMigration_BackupContentMatchesSource()
    {
        string dbPath = CreateFakeDb(name: "media.db");

        DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: 1);

        string[] backups = Directory.GetFiles(path: _backupDir, searchPattern: "media.*.db");
        using SqliteConnection backup = new(connectionString: $"Data Source={backups[0]}; Pooling=False;");
        backup.Open();
        using SqliteCommand query = backup.CreateCommand();
        query.CommandText = "SELECT content FROM marker;";
        Assert.Equal(expected: "fake sqlite content", actual: (string?)query.ExecuteScalar());
    }

    [Fact]
    public void BackupBeforeMigration_BackupFileNameContainsTimestamp()
    {
        string dbPath = CreateFakeDb(name: "queue.db");

        DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: 1);

        string[] backups = Directory.GetFiles(path: _backupDir, searchPattern: "queue.*.db");
        string fileName = Path.GetFileName(path: backups[0]);

        // Pattern: queue.<yyyyMMddHHmmss>.db — 14-digit timestamp between dots
        string[] parts = fileName.Split(separator: '.');
        Assert.Equal(expected: 3, actual: parts.Length);
        Assert.Equal(expected: "queue", actual: parts[0]);
        Assert.Equal(expected: "db", actual: parts[2]);
        Assert.Equal(expected: 14, actual: parts[1].Length);
        Assert.True(condition: long.TryParse(s: parts[1], result: out _), userMessage: "Timestamp portion must be numeric");
    }

    [Fact]
    public void BackupBeforeMigration_ExceedsRetainCount_PrunesOldest()
    {
        DatabaseBackupService.RetainCount = 3;
        string dbPath = CreateFakeDb(name: "app.db");

        // Seed four existing backup files with ascending timestamps
        Directory.CreateDirectory(path: _backupDir);
        for (int index = 1; index <= 4; index++)
        {
            string fake = Path.Combine(path1: _backupDir, path2: $"app.2026010100000{index}.db");
            File.WriteAllText(path: fake, contents: "old backup");
        }

        DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: 1);

        string[] remaining = Directory.GetFiles(path: _backupDir, searchPattern: "app.*.db");
        Assert.Equal(expected: 3, actual: remaining.Length);

        // The oldest (lowest timestamp) must have been deleted
        string[] fileNames = remaining.Select(selector: Path.GetFileName).ToArray()!;
        Assert.DoesNotContain(expected: "app.20260101000001.db", collection: fileNames);
    }

    [Fact]
    public void BackupBeforeMigration_BelowRetainCount_KeepsAll()
    {
        DatabaseBackupService.RetainCount = 5;
        string dbPath = CreateFakeDb(name: "media.db");

        // Seed two existing backup files
        Directory.CreateDirectory(path: _backupDir);
        File.WriteAllText(path: Path.Combine(path1: _backupDir, path2: "media.20260101000001.db"), contents: "old");
        File.WriteAllText(path: Path.Combine(path1: _backupDir, path2: "media.20260101000002.db"), contents: "old");

        DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: 1);

        string[] remaining = Directory.GetFiles(path: _backupDir, searchPattern: "media.*.db");
        Assert.Equal(expected: 3, actual: remaining.Length);
    }

    [Fact]
    public void BackupBeforeMigration_MultipleDbNames_PrunesIndependently()
    {
        DatabaseBackupService.RetainCount = 2;
        string mediaDb = CreateFakeDb(name: "media.db");
        string queueDb = CreateFakeDb(name: "queue.db");

        Directory.CreateDirectory(path: _backupDir);
        for (int index = 1; index <= 3; index++)
        {
            File.WriteAllText(path: Path.Combine(path1: _backupDir, path2: $"media.2026010100000{index}.db"), contents: "old");
            File.WriteAllText(path: Path.Combine(path1: _backupDir, path2: $"queue.2026010100000{index}.db"), contents: "old");
        }

        DatabaseBackupService.BackupBeforeMigration(dbPath: mediaDb, pendingMigrationCount: 1);
        DatabaseBackupService.BackupBeforeMigration(dbPath: queueDb, pendingMigrationCount: 1);

        string[] mediaBackups = Directory.GetFiles(path: _backupDir, searchPattern: "media.*.db");
        string[] queueBackups = Directory.GetFiles(path: _backupDir, searchPattern: "queue.*.db");

        Assert.Equal(expected: 2, actual: mediaBackups.Length);
        Assert.Equal(expected: 2, actual: queueBackups.Length);
    }

    [Fact]
    public void BackupBeforeMigration_WalResidentUncheckpointedRow_IsIncludedInBackup()
    {
        string dbPath = Path.Combine(path1: _tempDir, path2: "wal-media.db");
        string[] backups;

        using (SqliteConnection walConnection = new(connectionString: $"Data Source={dbPath}"))
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

            Assert.True(condition: File.Exists(path: dbPath + "-wal"));

            bool backedUp = DatabaseBackupService.BackupBeforeMigration(
                dbPath: dbPath,
                pendingMigrationCount: 1
            );

            Assert.True(condition: backedUp);
            backups = Directory.GetFiles(path: _backupDir, searchPattern: "wal-media.*.db");
            Assert.Single(collection: backups);
        }

        SqliteConnection.ClearAllPools();

        object? value;
        using (SqliteConnection verifyConnection = new(connectionString: $"Data Source={backups[0]}"))
        {
            verifyConnection.Open();
            using SqliteCommand select = verifyConnection.CreateCommand();
            select.CommandText = "SELECT Value FROM Probe;";
            value = select.ExecuteScalar();
        }

        SqliteConnection.ClearAllPools();

        Assert.Equal(expected: "wal-resident-row", actual: value);
    }

    [Fact]
    public void BackupBeforeMigration_InvalidBackupRoot_ReturnsFalseWithoutThrowing()
    {
        DatabaseBackupService.BackupRoot = "\0invalid\0path";
        string dbPath = CreateFakeDb(name: "media.db");

        bool result = DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: 1);

        // Must not throw — failure is non-fatal
        Assert.False(condition: result);
    }

    // ── BackupNow (unconditional — used by the daily backup cron job) ────────

    [Fact]
    public void BackupNow_DbExists_CreatesBackupFileRegardlessOfMigrations()
    {
        string dbPath = CreateFakeDb(name: "media.db");

        bool result = DatabaseBackupService.BackupNow(dbPath: dbPath, reason: "daily scheduled backup");

        Assert.True(condition: result);
        string[] backups = Directory.GetFiles(path: _backupDir, searchPattern: "media.*.db");
        Assert.Single(collection: backups);
    }

    [Fact]
    public void BackupNow_DbDoesNotExist_ReturnsFalseWithoutThrowing()
    {
        string nonExistentPath = Path.Combine(path1: _tempDir, path2: "nonexistent.db");

        bool result = DatabaseBackupService.BackupNow(dbPath: nonExistentPath, reason: "daily scheduled backup");

        Assert.False(condition: result);
        Assert.False(condition: Directory.Exists(path: _backupDir));
    }

    [Fact]
    public void BackupNow_RespectsRetainCountAcrossRepeatedCalls()
    {
        DatabaseBackupService.RetainCount = 2;
        string dbPath = CreateFakeDb(name: "media.db");

        Directory.CreateDirectory(path: _backupDir);
        for (int index = 1; index <= 3; index++)
            File.WriteAllText(
                path: Path.Combine(path1: _backupDir, path2: $"media.2026010100000{index}.db"),
                contents: "old backup"
            );

        DatabaseBackupService.BackupNow(dbPath: dbPath, reason: "daily scheduled backup");

        string[] remaining = Directory.GetFiles(path: _backupDir, searchPattern: "media.*.db");
        Assert.Equal(expected: 2, actual: remaining.Length);
    }
}
