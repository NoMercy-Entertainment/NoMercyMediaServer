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
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup.Maintenance;

/// <summary>
/// Reconciles the music <c>TitleSort</c> column with the current
/// <see cref="TitleSortHelper"/> algorithm. Runs deferred after boot in batches
/// and recomputes each row's sort key, writing only where the stored value has
/// drifted (a null from a pre-column import, or a value produced by an older
/// algorithm). This is what propagates a TitleSort rule change to an already
/// imported library — filling only nulls left every existing row stale. Once a
/// library matches the current algorithm every batch is a no-op write, so later
/// boots settle to plain reads.
/// </summary>
public static class TitleSortBackfill
{
    private const int BatchSize = 1000;

    public static Task RunAsync(CancellationToken ct = default) =>
        RunAsync(contextFactory: static () => new MediaContext(), ct: ct);

    public static async Task RunAsync(
        Func<MediaContext> contextFactory,
        CancellationToken ct = default
    )
    {
        try
        {
            int artists = await ReconcileArtistsAsync(contextFactory: contextFactory, ct: ct);
            int albums = await ReconcileAlbumsAsync(contextFactory: contextFactory, ct: ct);

            if (artists > 0 || albums > 0)
                Logger.Setup(
                    message: $"TitleSort reconcile complete: {artists} artists, {albums} albums updated",
                    level: LogEventLevel.Information
                );
        }
        catch (Exception e)
        {
            Logger.Setup(message: $"TitleSort reconcile failed: {e.Message}", level: LogEventLevel.Warning);
        }
    }

    private static async Task<int> ReconcileArtistsAsync(
        Func<MediaContext> contextFactory,
        CancellationToken ct
    )
    {
        int updated = 0;
        int offset = 0;
        while (!ct.IsCancellationRequested)
        {
            await using MediaContext context = contextFactory();
            // Offset paging over a stable Id order: the recompute mutates only
            // TitleSort, never Id, so rows never shift between pages. Guid cursor
            // comparison does not translate on SQLite, hence Skip/Take here.
            List<Artist> batch = await context
                .Artists.OrderBy(keySelector: artist => artist.Id)
                .Skip(count: offset)
                .Take(count: BatchSize)
                .ToListAsync(cancellationToken: ct);

            if (batch.Count == 0)
                break;

            foreach (Artist artist in batch)
            {
                string sort = artist.Name.TitleSort();
                if (artist.TitleSort != sort)
                {
                    artist.TitleSort = sort;
                    updated++;
                }
            }

            await context.SaveChangesAsync(cancellationToken: ct);
            offset += batch.Count;
        }

        return updated;
    }

    private static async Task<int> ReconcileAlbumsAsync(
        Func<MediaContext> contextFactory,
        CancellationToken ct
    )
    {
        int updated = 0;
        int offset = 0;
        while (!ct.IsCancellationRequested)
        {
            await using MediaContext context = contextFactory();
            List<Album> batch = await context
                .Albums.OrderBy(keySelector: album => album.Id)
                .Skip(count: offset)
                .Take(count: BatchSize)
                .ToListAsync(cancellationToken: ct);

            if (batch.Count == 0)
                break;

            foreach (Album album in batch)
            {
                string sort = album.Name.TitleSort();
                if (album.TitleSort != sort)
                {
                    album.TitleSort = sort;
                    updated++;
                }
            }

            await context.SaveChangesAsync(cancellationToken: ct);
            offset += batch.Count;
        }

        return updated;
    }
}
