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
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NoMercy.Database;

public class EntityBaseUpdatedAtInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new()
    )
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData: eventData, result: result, cancellationToken: cancellationToken);

        IEnumerable<Timestamps> entries = eventData
            .Context.ChangeTracker.Entries()
            .Where(predicate: e => e.State == EntityState.Modified)
            .Select(selector: e => e.Entity)
            .OfType<Timestamps>();

        foreach (Timestamps entry in entries)
        {
            // CreatedAt comes from SQLite's CURRENT_TIMESTAMP (UTC). Mixing
            // DateTime.Now here introduced a TZ drift on every save.
            entry.UpdatedAt = DateTime.UtcNow;
        }

        return await base.SavingChangesAsync(eventData: eventData, result: result, cancellationToken: cancellationToken);
    }
}
