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
using NoMercy.Database;
using NoMercyQueue.Core.Interfaces;
using DbConfiguration = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Queue.MediaServer.Configuration;

public class MediaConfigurationStore(IDbContextFactory<AppDbContext> contextFactory)
    : IConfigurationStore
{
    public string? GetValue(string key)
    {
        using AppDbContext context = contextFactory.CreateDbContext();
        DbConfiguration? config = context.Configuration.FirstOrDefault(predicate: c => c.Key == key);
        return config?.Value;
    }

    public void SetValue(string key, string value)
    {
        using AppDbContext context = contextFactory.CreateDbContext();
        DbConfiguration? existing = context.Configuration.FirstOrDefault(predicate: c => c.Key == key);
        if (existing is not null)
        {
            existing.Value = value;
        }
        else
        {
            context.Configuration.Add(entity: new() { Key = key, Value = value });
        }
        context.SaveChanges();
    }

    public async Task SetValueAsync(string key, string value, Guid? modifiedBy = null)
    {
        await using AppDbContext context = await contextFactory.CreateDbContextAsync();
        DbConfiguration? existing = await context.Configuration.FirstOrDefaultAsync(predicate: c =>
            c.Key == key
        );
        if (existing is not null)
        {
            existing.Value = value;
            existing.ModifiedBy = modifiedBy;
        }
        else
        {
            context.Configuration.Add(
                entity: new()
                {
                    Key = key,
                    Value = value,
                    ModifiedBy = modifiedBy,
                }
            );
        }
        await context.SaveChangesAsync();
    }

    public bool HasKey(string key)
    {
        using AppDbContext context = contextFactory.CreateDbContext();
        return context.Configuration.Any(predicate: c => c.Key == key);
    }
}
