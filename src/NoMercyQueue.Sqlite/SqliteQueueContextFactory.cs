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
using NoMercyQueue.Core.Interfaces;

namespace NoMercyQueue.Sqlite;

public static class SqliteQueueContextFactory
{
    public static IQueueContext Create(string databasePath)
    {
        DbContextOptions<QueueDbContext> options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseSqlite(connectionString: $"Data Source={databasePath}; Pooling=True; Foreign Keys=True;")
            .AddInterceptors(interceptors: new SqliteQueueConnectionInterceptor())
            .Options;

        QueueDbContext dbContext = new(options: options);
        dbContext.Database.EnsureCreated();

        return new SqliteQueueContext(context: dbContext);
    }
}
