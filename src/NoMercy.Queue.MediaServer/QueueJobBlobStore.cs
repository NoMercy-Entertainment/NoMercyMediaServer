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
using NoMercy.Database.Models.Queue;

namespace NoMercy.Queue.MediaServer;

public class QueueJobBlobStore(IDbContextFactory<QueueContext> contextFactory) : IQueueJobBlobStore
{
    public async Task WriteAsync(string key, string data)
    {
        await using QueueContext context = await contextFactory.CreateDbContextAsync();

        // First writer wins. The key names the data, so a second dispatch for the
        // same release is storing a copy of what is already there — and overwriting
        // would rewrite a megabyte per track of the album for no change.
        if (await context.QueueJobBlobs.AnyAsync(blob => blob.Key == key))
            return;

        context.QueueJobBlobs.Add(new() { Key = key, Data = data });

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another dispatch inserted the same key between the check and the
            // write. Both wanted the same bytes there, so the row that landed is
            // the row this one was going to write.
        }
    }

    public async Task<string?> ReadAsync(string key)
    {
        await using QueueContext context = await contextFactory.CreateDbContextAsync();

        return await context
            .QueueJobBlobs.AsNoTracking()
            .Where(blob => blob.Key == key)
            .Select(blob => blob.Data)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SweepUnreferencedAsync()
    {
        await using QueueContext context = await contextFactory.CreateDbContextAsync();

        // Anti-join against the queue rows' own key column. Digging the key back
        // out of the payloads instead would mean reading every payload in the
        // queue, which is the cost this whole table exists to avoid.
        return await context
            .QueueJobBlobs.Where(blob =>
                !context.QueueJobs.Any(job => job.SharedInputKey == blob.Key)
            )
            .ExecuteDeleteAsync();
    }
}
