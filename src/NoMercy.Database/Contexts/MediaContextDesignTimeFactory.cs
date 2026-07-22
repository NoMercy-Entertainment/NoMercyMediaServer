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
using Microsoft.EntityFrameworkCore.Design;

namespace NoMercy.Database;

public class MediaContextDesignTimeFactory : IDesignTimeDbContextFactory<MediaContext>
{
    public MediaContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<MediaContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(
            connectionString: "Data Source=media_designtime.db",
            sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
        );
        return new(options: optionsBuilder.Options);
    }
}
