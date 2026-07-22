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
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;

namespace NoMercy.Database;

public class AppDbContext : DbContext
{
    public DbSet<Configuration> Configuration { get; set; }

    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options: options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(selector: t => t.GetProperties())
            .Where(predicate: p => p.Name is "CreatedAt" or "UpdatedAt")
            .ToList()
            .ForEach(action: p => p.SetDefaultValueSql(value: "CURRENT_TIMESTAMP"));

        modelBuilder
            .Entity<Configuration>()
            .Property(propertyExpression: e => e.SecureValue)
            .HasConversion(convertToProviderExpression: v => TokenStore.EncryptToken(v), convertFromProviderExpression: v => TokenStore.DecryptToken(v));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite(connectionString: $"Data Source={AppFiles.AppDatabase}; Foreign Keys=True;");
        }
    }
}
