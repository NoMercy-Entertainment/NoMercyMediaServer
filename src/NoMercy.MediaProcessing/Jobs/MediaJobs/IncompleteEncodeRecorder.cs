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
using NoMercy.Database.Models.Encoder;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public sealed class IncompleteEncodeRecorder
{
    public async Task RecordAsync(
        MediaContext context,
        long mediaId,
        string folderId,
        string title,
        IReadOnlyList<string> missingKeys,
        string? lastError,
        int attemptsMade,
        CancellationToken ct
    )
    {
        string renditions = string.Join(separator: '\n', values: missingKeys);

        IncompleteEncode? existing = await context.IncompleteEncodes.FirstOrDefaultAsync(
            predicate: r => r.MediaId == mediaId && r.FolderId == folderId,
            cancellationToken: ct
        );

        if (existing is null)
        {
            DateTime now = DateTime.UtcNow;
            context.IncompleteEncodes.Add(
                entity: new()
                {
                    MediaId = mediaId,
                    FolderId = folderId,
                    Title = title,
                    MissingRenditions = renditions,
                    LastError = lastError,
                    AttemptsMade = attemptsMade,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                }
            );
        }
        else
        {
            existing.MissingRenditions = renditions;
            existing.LastError = lastError;
            existing.AttemptsMade = attemptsMade;
            existing.LastSeenAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task ClearAsync(
        MediaContext context,
        long mediaId,
        string folderId,
        CancellationToken ct
    )
    {
        await context
            .IncompleteEncodes.Where(predicate: r => r.MediaId == mediaId && r.FolderId == folderId)
            .ExecuteDeleteAsync(cancellationToken: ct);
    }
}
