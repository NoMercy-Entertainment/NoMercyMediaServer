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

        if (await PaletteBackfillState.IsCompleteAsync(appDb, CancellationToken.None))
            return;

        bool anyDispatched = false;

        foreach (string entityType in IntTypes)
        {
            bool dispatched = await ProcessIntTypeAsync(appDb, db, entityType);
            if (dispatched)
                anyDispatched = true;
        }

        foreach (string entityType in GuidTypes)
        {
            bool dispatched = await ProcessGuidTypeAsync(appDb, db, entityType);
            if (dispatched)
                anyDispatched = true;
        }

        if (!anyDispatched)
        {
            await PaletteBackfillState.SetCompleteAsync(appDb, CancellationToken.None);
            return;
        }

        // Re-enqueue self to continue the drain on the next poll cycle.
        QueueRunner.Current?.Dispatcher.Dispatch(
            new PaletteBackfillJob(),
            "palette",
            PalettePriority.BackfillCoordinator
        );
    }

    private static async Task<bool> ProcessIntTypeAsync(
        AppDbContext appDb,
        MediaContext db,
        string entityType
    )
    {
        long cursor = await PaletteBackfillState.GetCursorAsync(
            appDb,
            entityType,
            CancellationToken.None
        );

        List<(int Id, string? Palette)> rows = await GetIntPendingRowsAsync(db, entityType, cursor);
        if (rows.Count == 0)
            return false;

        QueueJobDispatcher? dispatcher = QueueRunner.Current?.Dispatcher;
        if (dispatcher is null)
            return false;

        foreach ((int id, string? _) in rows)
            dispatcher.Dispatch(
                new ColorPaletteJob(entityType, id.ToString()),
                "palette",
                PalettePriority.ForBackfill(entityType)
            );

        long newCursor = rows[^1].Id;
        await PaletteBackfillState.SetCursorAsync(
            appDb,
            entityType,
            newCursor,
            CancellationToken.None
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
            appDb,
            entityType,
            CancellationToken.None
        );

        List<(Guid Id, string? Palette)> rows = await GetGuidPendingRowsAsync(
            db,
            entityType,
            (int)offset
        );
        if (rows.Count == 0)
            return false;

        QueueJobDispatcher? dispatcher = QueueRunner.Current?.Dispatcher;
        if (dispatcher is null)
            return false;

        foreach ((Guid id, string? _) in rows)
            dispatcher.Dispatch(
                new ColorPaletteJob(entityType, id.ToString()),
                "palette",
                PalettePriority.ForBackfill(entityType)
            );

        long newOffset = offset + rows.Count;
        await PaletteBackfillState.SetCursorAsync(
            appDb,
            entityType,
            newOffset,
            CancellationToken.None
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
                .Movies.Where(m =>
                    m.Id > cursor
                    && (m._colorPalette == null || m._colorPalette == "" || m._colorPalette == "{}")
                )
                .OrderBy(m => m.Id)
                .Take(BatchSize)
                .Select(m => new ValueTuple<int, string?>(m.Id, m._colorPalette))
                .ToListAsync(),
            "tv" => db
                .Tvs.Where(t =>
                    t.Id > cursor
                    && (t._colorPalette == null || t._colorPalette == "" || t._colorPalette == "{}")
                )
                .OrderBy(t => t.Id)
                .Take(BatchSize)
                .Select(t => new ValueTuple<int, string?>(t.Id, t._colorPalette))
                .ToListAsync(),
            "season" => db
                .Seasons.Where(s =>
                    s.Id > cursor
                    && (s._colorPalette == null || s._colorPalette == "" || s._colorPalette == "{}")
                )
                .OrderBy(s => s.Id)
                .Take(BatchSize)
                .Select(s => new ValueTuple<int, string?>(s.Id, s._colorPalette))
                .ToListAsync(),
            "episode" => db
                .Episodes.Where(e =>
                    e.Id > cursor
                    && (e._colorPalette == null || e._colorPalette == "" || e._colorPalette == "{}")
                )
                .OrderBy(e => e.Id)
                .Take(BatchSize)
                .Select(e => new ValueTuple<int, string?>(e.Id, e._colorPalette))
                .ToListAsync(),
            "collection" => db
                .Collections.Where(c =>
                    c.Id > cursor
                    && (c._colorPalette == null || c._colorPalette == "" || c._colorPalette == "{}")
                )
                .OrderBy(c => c.Id)
                .Take(BatchSize)
                .Select(c => new ValueTuple<int, string?>(c.Id, c._colorPalette))
                .ToListAsync(),
            "person" => db
                .People.Where(p =>
                    p.Id > cursor
                    && (p._colorPalette == null || p._colorPalette == "" || p._colorPalette == "{}")
                )
                .OrderBy(p => p.Id)
                .Take(BatchSize)
                .Select(p => new ValueTuple<int, string?>(p.Id, p._colorPalette))
                .ToListAsync(),
            "recommendation" => db
                .Recommendations.Where(r =>
                    r.Id > cursor
                    && (r._colorPalette == null || r._colorPalette == "" || r._colorPalette == "{}")
                )
                .OrderBy(r => r.Id)
                .Take(BatchSize)
                .Select(r => new ValueTuple<int, string?>(r.Id, r._colorPalette))
                .ToListAsync(),
            "similar" => db
                .Similar.Where(s =>
                    s.Id > cursor
                    && (s._colorPalette == null || s._colorPalette == "" || s._colorPalette == "{}")
                )
                .OrderBy(s => s.Id)
                .Take(BatchSize)
                .Select(s => new ValueTuple<int, string?>(s.Id, s._colorPalette))
                .ToListAsync(),
            "image" => db
                .Images.Where(i =>
                    i.Id > cursor
                    && i.Site == "https://image.tmdb.org/t/p/"
                    && (i._colorPalette == null || i._colorPalette == "" || i._colorPalette == "{}")
                )
                .OrderBy(i => i.Id)
                .Take(BatchSize)
                .Select(i => new ValueTuple<int, string?>(i.Id, i._colorPalette))
                .ToListAsync(),
            _ => Task.FromResult(new List<(int, string?)>()),
        };

    private static Task<List<(Guid Id, string? Palette)>> GetGuidPendingRowsAsync(
        MediaContext db,
        string entityType,
        int offset
    ) =>
        entityType switch
        {
            "artist" => db
                .Artists.Where(a =>
                    a._colorPalette == null || a._colorPalette == "" || a._colorPalette == "{}"
                )
                .OrderBy(a => a.Id)
                .Skip(offset)
                .Take(BatchSize)
                .Select(a => new ValueTuple<Guid, string?>(a.Id, a._colorPalette))
                .ToListAsync(),
            "album" => db
                .Albums.Where(a =>
                    a._colorPalette == null || a._colorPalette == "" || a._colorPalette == "{}"
                )
                .OrderBy(a => a.Id)
                .Skip(offset)
                .Take(BatchSize)
                .Select(a => new ValueTuple<Guid, string?>(a.Id, a._colorPalette))
                .ToListAsync(),
            "track" => db
                .Tracks.Where(t =>
                    t._colorPalette == null || t._colorPalette == "" || t._colorPalette == "{}"
                )
                .OrderBy(t => t.Id)
                .Skip(offset)
                .Take(BatchSize)
                .Select(t => new ValueTuple<Guid, string?>(t.Id, t._colorPalette))
                .ToListAsync(),
            "playlist" => db
                .Playlists.Where(p =>
                    p._colorPalette == null || p._colorPalette == "" || p._colorPalette == "{}"
                )
                .OrderBy(p => p.Id)
                .Skip(offset)
                .Take(BatchSize)
                .Select(p => new ValueTuple<Guid, string?>(p.Id, p._colorPalette))
                .ToListAsync(),
            "releasegroup" => db
                .ReleaseGroups.Where(r =>
                    r._colorPalette == null || r._colorPalette == "" || r._colorPalette == "{}"
                )
                .OrderBy(r => r.Id)
                .Skip(offset)
                .Take(BatchSize)
                .Select(r => new ValueTuple<Guid, string?>(r.Id, r._colorPalette))
                .ToListAsync(),
            _ => Task.FromResult(new List<(Guid, string?)>()),
        };
}
