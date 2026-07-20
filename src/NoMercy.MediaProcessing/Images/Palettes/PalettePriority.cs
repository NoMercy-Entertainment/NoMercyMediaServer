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

namespace NoMercy.MediaProcessing.Images.Palettes;

// The queue reserves work by OrderByDescending(Priority) (SqliteQueueContext),
// so a HIGHER number is dequeued FIRST. The tiers below run in this order:
// on-demand → the entity a live import is about (movie/tv/episode) → that
// import's images → backfill of main entities → backfill of images → the
// backfill coordinator itself. The previous values were inverted against the
// descending reserve order, so a live import painted its images (8) before the
// show/movie/episode (4) it belongs to, and the coordinator (20) outranked the
// live imports its own comment said it must yield to.
public static class PalettePriority
{
    // A user is waiting on this palette right now — outranks everything.
    public const int OnDemand = 100;

    // The self-re-enqueuing drain loop yields to every real palette job so live
    // imports and dispatched backfill work always reserve ahead of the next batch.
    public const int BackfillCoordinator = 10;

    private static readonly HashSet<string> MainTypes = new()
    {
        "movie",
        "tv",
        "season",
        "episode",
        "person",
        "collection",
        "artist",
        "album",
    };

    public static bool IsMain(string entityType) => MainTypes.Contains(entityType);

    // A live import: the main entity (the thing the user just added) paints
    // before its own images, and the whole import outranks any backfill.
    public static int ForImport(string entityType) => IsMain(entityType) ? 80 : 70;

    // Backfilling the existing library: below every live import, main entities
    // still ahead of their images.
    public static int ForBackfill(string entityType) => IsMain(entityType) ? 40 : 30;
}
