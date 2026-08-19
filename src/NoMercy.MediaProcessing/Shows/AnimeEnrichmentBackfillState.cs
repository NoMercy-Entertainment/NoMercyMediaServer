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
using NoMercy.Database.Models.Common;

namespace NoMercy.MediaProcessing.Shows;

/// <summary>
/// Reads and writes the one-shot completion flag for
/// <see cref="AnimeEnrichmentBackfillJob"/> in the <see cref="AppDbContext"/>
/// Configuration table, mirroring <c>PaletteBackfillState</c>'s pattern. Unlike
/// the palette backfill, this drain has no per-type cursor: a library's anime
/// count is small enough that a single pass over the anime-typed Tvs/Movies is
/// fine, and the flag alone is enough to keep the job from re-running on every boot.
/// </summary>
public class AnimeEnrichmentBackfillState
{
    private const string CompleteKey = "anime_enrichment_backfill_complete";

    public static async Task<bool> IsCompleteAsync(CancellationToken ct)
    {
        await using AppDbContext db = new();
        string? value = await db
            .Configuration.Where(c => c.Key == CompleteKey)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);
        return value == "true";
    }

    public static async Task SetCompleteAsync(CancellationToken ct)
    {
        await using AppDbContext db = new();
        Configuration? existing = await db.Configuration.FirstOrDefaultAsync(
            c => c.Key == CompleteKey,
            ct
        );
        if (existing is null)
            db.Configuration.Add(new() { Key = CompleteKey, Value = "true" });
        else
            existing.Value = "true";

        await db.SaveChangesAsync(ct);
    }
}
