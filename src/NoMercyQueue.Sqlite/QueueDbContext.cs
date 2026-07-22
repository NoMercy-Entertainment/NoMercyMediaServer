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

using Microsoft.EntityFrameworkCore;
using NoMercyQueue.Sqlite.Entities;

namespace NoMercyQueue.Sqlite;

internal class QueueDbContext : DbContext
{
    public QueueDbContext(DbContextOptions<QueueDbContext> options)
        : base(options: options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder: configurationBuilder);
        configurationBuilder.Properties<string>().HaveMaxLength(maxLength: 256);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(selector: t => t.GetProperties())
            .Where(predicate: p => p.Name is "CreatedAt" or "UpdatedAt")
            .ToList()
            .ForEach(action: p => p.SetDefaultValueSql(value: "CURRENT_TIMESTAMP"));

        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(selector: t => t.GetForeignKeys())
            .ToList()
            .ForEach(action: p => p.DeleteBehavior = DeleteBehavior.Cascade);

        modelBuilder.Entity<QueueJobEntity>().Property(propertyExpression: j => j.Payload).HasMaxLength(maxLength: 4096);

        base.OnModelCreating(modelBuilder: modelBuilder);
    }

    public DbSet<QueueJobEntity> QueueJobs { get; set; }
    public DbSet<FailedJobEntity> FailedJobs { get; set; }
    public DbSet<CronJobEntity> CronJobs { get; set; }
}
