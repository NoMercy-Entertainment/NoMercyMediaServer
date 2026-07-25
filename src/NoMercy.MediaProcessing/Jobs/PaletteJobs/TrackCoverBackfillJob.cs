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
using NoMercy.MediaProcessing.Images.Palettes;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

/// <summary>
/// One-shot, self-continuing repair that copies an album's cover onto any track
/// that has none of its own. A track palette is derived from the album cover, so
/// the displayed cover must match — otherwise a track renders album colours over a
/// blank tile. Cover is a cheap string copy (no image decode), so this drains in
/// large batches on the low-priority palette queue and stops when none remain.
/// </summary>
[Serializable]
public class TrackCoverBackfillJob : IShouldQueue
{
    public string QueueName => "palette";

    public int Priority => PalettePriority.BackfillCoordinator;

    private const int BatchSize = 500;

    public TrackCoverBackfillJob() { }

    public async Task Handle()
    {
        await using MediaContext db = new();

        List<TrackAlbumCover> pairs = await db
            .AlbumTrack.Where(at => at.Track.Cover == null && at.Album.Cover != null)
            .Take(BatchSize)
            .Select(at => new TrackAlbumCover(at.TrackId, at.Album.Cover!))
            .ToListAsync();

        if (pairs.Count == 0)
            return;

        foreach (TrackAlbumCover pair in pairs)
            await db
                .Tracks.Where(t => t.Id == pair.TrackId && t.Cover == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Cover, pair.Cover));

        // The pending filter shrinks every pass, so re-enqueueing self terminates
        // once all null covers are filled.
        QueueRunner.Current?.Dispatcher.Dispatch(
            new TrackCoverBackfillJob(),
            "palette",
            PalettePriority.BackfillCoordinator
        );
    }

    private sealed record TrackAlbumCover(Guid TrackId, string Cover);
}
