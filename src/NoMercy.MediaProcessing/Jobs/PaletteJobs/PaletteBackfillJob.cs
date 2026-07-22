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
using QueueJobDispatcher = NoMercyQueue.JobDispatcher;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

[Serializable]
public class PaletteBackfillJob : IShouldQueue
{
    public string QueueName => "palette";

    // Lowest priority so live imports and on-demand requests always drain first.
    public int Priority => PalettePriority.BackfillCoordinator;

    private const int BatchSize = 200;

    // Bumped whenever a new entity type joins the backfill so the one-shot drain
    // re-opens for existing libraries. v2 added "track".
    public const int CurrentVersion = 2;

    // Entity types and their PK kind (int vs Guid)
    private static readonly string[] IntTypes =
    [
        "movie",
        "tv",
        "season",
        "episode",
        "collection",
        "person",
        "recommendation",
        "similar",
        "image",
    ];

    private static readonly string[] GuidTypes =
    [
        "artist",
        "album",
        "track",
        "playlist",
        "releasegroup",
    ];

    public static IReadOnlyList<string> AllTypes => [.. IntTypes, .. GuidTypes];

    public PaletteBackfillJob() { }

    public async Task Handle()
    {
        await using AppDbContext appDb = new();
        await using MediaContext db = new();

        if (await PaletteBackfillState.IsCompleteAsync(db: appDb, ct: CancellationToken.None))
            return;

        bool anyDispatched = false;

        foreach (string entityType in IntTypes)
        {
            bool dispatched = await ProcessIntTypeAsync(appDb: appDb, db: db, entityType: entityType);
            if (dispatched)
                anyDispatched = true;
        }

        foreach (string entityType in GuidTypes)
        {
            bool dispatched = await ProcessGuidTypeAsync(appDb: appDb, db: db, entityType: entityType);
            if (dispatched)
                anyDispatched = true;
        }

        if (!anyDispatched)
        {
            await PaletteBackfillState.SetCompleteAsync(db: appDb, ct: CancellationToken.None);
            return;
        }

        // Re-enqueue self to continue the drain on the next poll cycle.
        QueueRunner.Current?.Dispatcher.Dispatch(
            job: new PaletteBackfillJob(),
            onQueue: "palette",
            priority: PalettePriority.BackfillCoordinator
        );
    }

    private static async Task<bool> ProcessIntTypeAsync(
        AppDbContext appDb,
        MediaContext db,
        string entityType
    )
    {
        long cursor = await PaletteBackfillState.GetCursorAsync(
            db: appDb,
            entityType: entityType,
            ct: CancellationToken.None
        );

        List<(int Id, string? Palette)> rows = await GetIntPendingRowsAsync(db: db, entityType: entityType, cursor: cursor);
        if (rows.Count == 0)
            return false;

        QueueJobDispatcher? dispatcher = QueueRunner.Current?.Dispatcher;
        if (dispatcher is null)
            return false;

        foreach ((int id, string? _) in rows)
            dispatcher.Dispatch(
                job: new ColorPaletteJob(entityType: entityType, entityId: id.ToString()),
                onQueue: "palette",
                priority: PalettePriority.ForBackfill(entityType: entityType)
            );

        long newCursor = rows[^1].Id;
        await PaletteBackfillState.SetCursorAsync(
            db: appDb,
            entityType: entityType,
            cursor: newCursor,
            ct: CancellationToken.None
        );

        return rows.Count == BatchSize;
    }

    private static async Task<bool> ProcessGuidTypeAsync(
        AppDbContext appDb,
        MediaContext db,
        string entityType
    )
    {
        long offset = await PaletteBackfillState.GetCursorAsync(
            db: appDb,
            entityType: entityType,
            ct: CancellationToken.None
        );

        List<(Guid Id, string? Palette)> rows = await GetGuidPendingRowsAsync(
            db: db,
            entityType: entityType,
            offset: (int)offset
        );
        if (rows.Count == 0)
            return false;

        QueueJobDispatcher? dispatcher = QueueRunner.Current?.Dispatcher;
        if (dispatcher is null)
            return false;

        foreach ((Guid id, string? _) in rows)
            dispatcher.Dispatch(
                job: new ColorPaletteJob(entityType: entityType, entityId: id.ToString()),
                onQueue: "palette",
                priority: PalettePriority.ForBackfill(entityType: entityType)
            );

        long newOffset = offset + rows.Count;
        await PaletteBackfillState.SetCursorAsync(
            db: appDb,
            entityType: entityType,
            cursor: newOffset,
            ct: CancellationToken.None
        );

        return rows.Count == BatchSize;
    }

    private static Task<List<(int Id, string? Palette)>> GetIntPendingRowsAsync(
        MediaContext db,
        string entityType,
        long cursor
    ) =>
        entityType switch
        {
            "movie" => db
                .Movies.Where(predicate: m =>
                    m.Id > cursor && (m._colorPalette == null || m._colorPalette == "")
                )
                .OrderBy(keySelector: m => m.Id)
                .Take(count: BatchSize)
                .Select(selector: m => new ValueTuple<int, string?>(m.Id, m._colorPalette))
                .ToListAsync(),
            "tv" => db
                .Tvs.Where(predicate: t => t.Id > cursor && (t._colorPalette == null || t._colorPalette == ""))
                .OrderBy(keySelector: t => t.Id)
                .Take(count: BatchSize)
                .Select(selector: t => new ValueTuple<int, string?>(t.Id, t._colorPalette))
                .ToListAsync(),
            "season" => db
                .Seasons.Where(predicate: s =>
                    s.Id > cursor && (s._colorPalette == null || s._colorPalette == "")
                )
                .OrderBy(keySelector: s => s.Id)
                .Take(count: BatchSize)
                .Select(selector: s => new ValueTuple<int, string?>(s.Id, s._colorPalette))
                .ToListAsync(),
            "episode" => db
                .Episodes.Where(predicate: e =>
                    e.Id > cursor && (e._colorPalette == null || e._colorPalette == "")
                )
                .OrderBy(keySelector: e => e.Id)
                .Take(count: BatchSize)
                .Select(selector: e => new ValueTuple<int, string?>(e.Id, e._colorPalette))
                .ToListAsync(),
            "collection" => db
                .Collections.Where(predicate: c =>
                    c.Id > cursor && (c._colorPalette == null || c._colorPalette == "")
                )
                .OrderBy(keySelector: c => c.Id)
                .Take(count: BatchSize)
                .Select(selector: c => new ValueTuple<int, string?>(c.Id, c._colorPalette))
                .ToListAsync(),
            "person" => db
                .People.Where(predicate: p =>
                    p.Id > cursor && (p._colorPalette == null || p._colorPalette == "")
                )
                .OrderBy(keySelector: p => p.Id)
                .Take(count: BatchSize)
                .Select(selector: p => new ValueTuple<int, string?>(p.Id, p._colorPalette))
                .ToListAsync(),
            "recommendation" => db
                .Recommendations.Where(predicate: r =>
                    r.Id > cursor && (r._colorPalette == null || r._colorPalette == "")
                )
                .OrderBy(keySelector: r => r.Id)
                .Take(count: BatchSize)
                .Select(selector: r => new ValueTuple<int, string?>(r.Id, r._colorPalette))
                .ToListAsync(),
            "similar" => db
                .Similar.Where(predicate: s =>
                    s.Id > cursor && (s._colorPalette == null || s._colorPalette == "")
                )
                .OrderBy(keySelector: s => s.Id)
                .Take(count: BatchSize)
                .Select(selector: s => new ValueTuple<int, string?>(s.Id, s._colorPalette))
                .ToListAsync(),
            "image" => db
                .Images.Where(predicate: i =>
                    i.Id > cursor
                    && i.Site == "https://image.tmdb.org/t/p/"
                    && (i._colorPalette == null || i._colorPalette == "")
                )
                .OrderBy(keySelector: i => i.Id)
                .Take(count: BatchSize)
                .Select(selector: i => new ValueTuple<int, string?>(i.Id, i._colorPalette))
                .ToListAsync(),
            _ => Task.FromResult(result: new List<(int, string?)>()),
        };

    private static Task<List<(Guid Id, string? Palette)>> GetGuidPendingRowsAsync(
        MediaContext db,
        string entityType,
        int offset
    ) =>
        entityType switch
        {
            "artist" => db
                .Artists.Where(predicate: a => a._colorPalette == null || a._colorPalette == "")
                .OrderBy(keySelector: a => a.Id)
                .Skip(count: offset)
                .Take(count: BatchSize)
                .Select(selector: a => new ValueTuple<Guid, string?>(a.Id, a._colorPalette))
                .ToListAsync(),
            "album" => db
                .Albums.Where(predicate: a => a._colorPalette == null || a._colorPalette == "")
                .OrderBy(keySelector: a => a.Id)
                .Skip(count: offset)
                .Take(count: BatchSize)
                .Select(selector: a => new ValueTuple<Guid, string?>(a.Id, a._colorPalette))
                .ToListAsync(),
            "track" => db
                .Tracks.Where(predicate: t => t._colorPalette == null || t._colorPalette == "")
                .OrderBy(keySelector: t => t.Id)
                .Skip(count: offset)
                .Take(count: BatchSize)
                .Select(selector: t => new ValueTuple<Guid, string?>(t.Id, t._colorPalette))
                .ToListAsync(),
            "playlist" => db
                .Playlists.Where(predicate: p => p._colorPalette == null || p._colorPalette == "")
                .OrderBy(keySelector: p => p.Id)
                .Skip(count: offset)
                .Take(count: BatchSize)
                .Select(selector: p => new ValueTuple<Guid, string?>(p.Id, p._colorPalette))
                .ToListAsync(),
            "releasegroup" => db
                .ReleaseGroups.Where(predicate: r => r._colorPalette == null || r._colorPalette == "")
                .OrderBy(keySelector: r => r.Id)
                .Skip(count: offset)
                .Take(count: BatchSize)
                .Select(selector: r => new ValueTuple<Guid, string?>(r.Id, r._colorPalette))
                .ToListAsync(),
            _ => Task.FromResult(result: new List<(Guid, string?)>()),
        };
}
