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
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.Name is "CreatedAt" or "UpdatedAt")
            .ToList()
            .ForEach(p => p.SetDefaultValueSql("CURRENT_TIMESTAMP"));

        modelBuilder
            .Entity<Configuration>()
            .Property(e => e.SecureValue)
            .HasConversion(v => TokenStore.EncryptToken(v), v => TokenStore.DecryptToken(v));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={AppFiles.AppDatabase}; Foreign Keys=True;");
        }
    }
}
