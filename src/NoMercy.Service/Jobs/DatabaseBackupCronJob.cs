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

using NoMercy.NmSystem.Information;
using NoMercy.Service.Seeds;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Service.Jobs;

/// <summary>
/// Takes a daily snapshot of every SQLite database under the data root,
/// independent of <see cref="DatabaseBackupService.BackupBeforeMigration"/> —
/// that path only fires when a migration is pending, which on a stable install
/// can be months apart. Without this job, corruption between migrations (a
/// crash, an unclean shutdown) has no recent recovery point to fall back to.
/// </summary>
public class DatabaseBackupCronJob : ICronJobExecutor
{
    private readonly ILogger<DatabaseBackupCronJob> _logger;
    private readonly string[] _dbPaths;

    public string CronExpression => new CronExpressionBuilder().Daily(4);
    public string JobName => "Daily Database Backup";

    public DatabaseBackupCronJob(ILogger<DatabaseBackupCronJob> logger, string[]? dbPaths = null)
    {
        _logger = logger;
        _dbPaths =
            dbPaths ?? [AppFiles.MediaDatabase, AppFiles.QueueDatabase, AppFiles.AppDatabase];
    }

    public Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        int backedUp = 0;
        foreach (string dbPath in _dbPaths)
            if (DatabaseBackupService.BackupNow(dbPath, "daily scheduled backup"))
                backedUp++;

        _logger.LogInformation(
            "Daily database backup complete: {BackedUp}/{Total} databases backed up", [backedUp, _dbPaths.Length]
        );

        return Task.CompletedTask;
    }
}
