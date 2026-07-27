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
using NoMercy.Database.Models.Security;

namespace NoMercy.Data.Security;

// One row per address, not an audit trail: the question this table answers is
// "is this address banned, and until when". The history of what it did lives in
// the activity log.
public class IpBanRepository(IDbContextFactory<MediaContext> contextFactory) : IIpBanRepository
{
    public async Task<List<IpBan>> ActiveAsync(DateTime now, CancellationToken ct)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        return await context
            .IpBans.AsNoTracking()
            .Where(ban => ban.ExpiresAt > now)
            .OrderByDescending(ban => ban.BannedAt)
            .ToListAsync(ct);
    }

    public async Task<IpBan?> FindActiveAsync(string address, DateTime now, CancellationToken ct)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        return await context
            .IpBans.AsNoTracking()
            .FirstOrDefaultAsync(ban => ban.Address == address && ban.ExpiresAt > now, ct);
    }

    public async Task<int> PriorBanCountAsync(string address, CancellationToken ct)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        return await context
                .IpBans.AsNoTracking()
                .Where(ban => ban.Address == address)
                .MaxAsync(ban => (int?)ban.BanNumber, ct)
            ?? 0;
    }

    public async Task<IpBan> UpsertAsync(IpBan ban, CancellationToken ct)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        IpBan? existing = await context.IpBans.FirstOrDefaultAsync(
            row => row.Address == ban.Address,
            ct
        );

        if (existing is null)
        {
            context.IpBans.Add(ban);
            await context.SaveChangesAsync(ct);
            return ban;
        }

        existing.Reason = ban.Reason;
        existing.LastPath = ban.LastPath;
        existing.OffenceCount = ban.OffenceCount;
        existing.BanNumber = ban.BanNumber;
        existing.BannedAt = ban.BannedAt;
        existing.ExpiresAt = ban.ExpiresAt;
        existing.Manual = ban.Manual;

        await context.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> RemoveAsync(string address, CancellationToken ct)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        int removed = await context
            .IpBans.Where(ban => ban.Address == address)
            .ExecuteDeleteAsync(ct);

        return removed > 0;
    }

    public async Task<int> PurgeExpiredAsync(DateTime cutoff, CancellationToken ct)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        return await context.IpBans.Where(ban => ban.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
