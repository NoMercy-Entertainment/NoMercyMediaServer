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
// so a HIGHER number is dequeued FIRST. Values are kept small (0-10, matching
// the queue's own priority scale) and are UI-first: the entity a viewer sees
// first paints first. Four bands, highest to lowest:
//   OnDemand (10)            — a user is waiting on this palette right now.
//   ForImport   (6-9)        — the entity a live import just added.
//   ForBackfill (2-5)        — draining the existing library in the background.
//   BackfillCoordinator (1)  — the self-re-enqueuing drain loop; yields to
//                              every real job above so it never starves them.
// Within the ForImport/ForBackfill bands, entityType picks a rank (0-3) added
// on top of the band's base, so the same UI hierarchy repeats in both bands:
//   3 — movie, tv, artist, album   (top-level, always shown first)
//   2 — episode, track             (played/browsed inside the above)
//   1 — season                     (a grouping, rarely shown on its own)
//   0 — person, collection, image, and anything else (lowest — supporting art)
// Every ForImport value outranks every ForBackfill value, so a live import
// always drains ahead of backfill regardless of entity type.
public static class PalettePriority
{
    // A user is waiting on this palette right now — outranks everything.
    public const int OnDemand = 10;

    // The self-re-enqueuing drain loop yields to every real palette job so live
    // imports and dispatched backfill work always reserve ahead of the next batch.
    public const int BackfillCoordinator = 1;

    private const int ImportBase = 6;
    private const int BackfillBase = 2;

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
        "track",
    };

    private static readonly Dictionary<string, int> EntityRank = new()
    {
        ["movie"] = 3,
        ["tv"] = 3,
        ["artist"] = 3,
        ["album"] = 3,
        ["episode"] = 2,
        ["track"] = 2,
        ["season"] = 1,
        ["person"] = 0,
        ["collection"] = 0,
        ["image"] = 0,
    };

    public static bool IsMain(string entityType) => MainTypes.Contains(entityType);

    private static int RankOf(string entityType) => EntityRank.GetValueOrDefault(entityType, 0);

    // A live import: the entity type's UI rank decides paint order, and the
    // whole band outranks any backfill.
    public static int ForImport(string entityType) => ImportBase + RankOf(entityType);

    // Backfilling the existing library: same UI-rank ordering as ForImport,
    // shifted below every live import.
    public static int ForBackfill(string entityType) => BackfillBase + RankOf(entityType);
}
