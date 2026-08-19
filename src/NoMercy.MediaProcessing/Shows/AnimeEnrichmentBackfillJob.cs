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
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.NmSystem.Extensions;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Shows;

/// <summary>
/// One-off backfill for libraries that already had anime imported before this
/// feature shipped. Always enqueues provider calls at low priority
/// (priority: false) through the shared rate-limited provider clients, so a
/// large backfill (potentially thousands of titles, serialized through
/// AniList's rate cap) never delays live per-item enrichment during a
/// concurrent library scan.
///
/// Drains in small, cursor-tracked batches and re-enqueues itself, mirroring
/// <c>PaletteBackfillJob</c>: a restart resumes at the last completed batch
/// instead of re-walking every anime title from the start.
///
/// Dispatched once, on boot, by <c>AnimeEnrichmentBackfillStartupService</c>
/// via the "extras" queue at the same priority as its ShowExtrasJob/
/// MovieExtrasJob siblings (not the queue's absolute floor - see
/// <see cref="Priority"/>), and re-checks the completion flag itself so a
/// redundant dispatch is a no-op.
/// </summary>
[Serializable]
public class AnimeEnrichmentBackfillJob : IShouldQueue, IJobStorageInjector
{
    public string QueueName => "extras";

    // Same tier as ShowExtrasJob/MovieExtrasJob (priority 1), not the queue's
    // absolute floor (0): a floor priority means every self-redispatch below
    // sorts this job strictly behind EVERY other extras-queue job, including
    // ones enqueued after it - on a live server the backlog never actually
    // empties, so priority 0 here means the backfill effectively never runs
    // again after its first batch. Priority 1 lets it interleave FIFO with
    // its siblings while still never contending with import/live queues,
    // which are separate queues entirely.
    public int Priority => 1;

    private const int BatchSize = 25;

    private IAnimeEnrichmentService? _animeEnrichmentService;
    private IDbContextFactory<MediaContext>? _contextFactory;

    /// <summary>Used by tests and any direct caller that already has the service.</summary>
    public AnimeEnrichmentBackfillJob(IAnimeEnrichmentService animeEnrichmentService)
    {
        _animeEnrichmentService = animeEnrichmentService;
    }

    /// <summary>Required by the queue for deserialization; services are supplied via <see cref="InjectStorageServices"/>.</summary>
    public AnimeEnrichmentBackfillJob() { }

    // Purely a dedup-breaker: the queue drops a Dispatch() whose serialized
    // payload matches an already-present row (JobQueue.Enqueue's JobExists
    // check), and this job's own reserved row is STILL PRESENT while Handle()
    // runs - the worker only deletes it in a finally block after Handle()
    // returns (QueueWorker.cs). A parameterless AnimeEnrichmentBackfillJob()
    // therefore always collides with itself on self-redispatch: the insert
    // silently no-ops, the worker then deletes the old row, and the backfill
    // dies with zero trace - verified live, reproduced twice. Stamping the
    // cursor this batch just advanced to makes every redispatch's payload
    // provably different from the row it is dispatched from inside.
    public int? DispatchedAfterTvCursor { get; set; }
    public int? DispatchedAfterMovieCursor { get; set; }

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        _animeEnrichmentService ??= serviceProvider.GetRequiredService<IAnimeEnrichmentService>();
        _contextFactory ??= serviceProvider.GetRequiredService<IDbContextFactory<MediaContext>>();
    }

    public async Task Handle()
    {
        if (_animeEnrichmentService is null || _contextFactory is null)
            return;

        await using AppDbContext appDb = new();
        if (await AnimeEnrichmentBackfillState.IsCompleteAsync(appDb, CancellationToken.None))
            return;

        await using MediaContext context = await _contextFactory.CreateDbContextAsync();

        int? tvCursor = await ProcessTvBatchAsync(appDb, context);
        int? movieCursor = await ProcessMovieBatchAsync(appDb, context);

        if (tvCursor is null && movieCursor is null)
        {
            await AnimeEnrichmentBackfillState.SetCompleteAsync(appDb, CancellationToken.None);
            return;
        }

        QueueRunner.Current?.Dispatcher.Dispatch(
            new AnimeEnrichmentBackfillJob
            {
                DispatchedAfterTvCursor = tvCursor,
                DispatchedAfterMovieCursor = movieCursor,
            },
            "extras",
            Priority
        );
    }

    // Returns the batch's last-processed tv id, or null if the tv sweep is
    // exhausted (no rows past the cursor) - the return value doubles as this
    // dispatch's dedup-breaking stamp (see DispatchedAfterTvCursor).
    private async Task<int?> ProcessTvBatchAsync(AppDbContext appDb, MediaContext context)
    {
        int cursor = await AnimeEnrichmentBackfillState.GetCursorAsync(
            appDb,
            "tv",
            CancellationToken.None
        );

        List<TvProjection> rows = await context
            .Tvs.AsNoTracking()
            .Where(tv => tv.Library.Type == "anime" && tv.Id > cursor)
            .OrderBy(tv => tv.Id)
            .Take(BatchSize)
            .Select(tv => new TvProjection(tv.Id, tv.Title, tv.FirstAirDate, tv.OriginCountry))
            .ToListAsync();

        if (rows.Count == 0)
            return null;

        foreach (TvProjection row in rows)
            await _animeEnrichmentService!.EnrichTvAsync(
                row.Id,
                row.Title,
                row.FirstAirDate.ParseYear(),
                row.OriginCountry is not null ? [row.OriginCountry] : null,
                false
            );

        await AnimeEnrichmentBackfillState.SetCursorAsync(
            appDb,
            "tv",
            rows[^1].Id,
            CancellationToken.None
        );
        return rows[^1].Id;
    }

    // Returns the batch's last-processed movie id, or null if the movie
    // sweep is exhausted - see ProcessTvBatchAsync.
    private async Task<int?> ProcessMovieBatchAsync(AppDbContext appDb, MediaContext context)
    {
        int cursor = await AnimeEnrichmentBackfillState.GetCursorAsync(
            appDb,
            "movie",
            CancellationToken.None
        );

        List<MovieProjection> rows = await context
            .Movies.AsNoTracking()
            .Where(movie => movie.Library.Type == "anime" && movie.Id > cursor)
            .OrderBy(movie => movie.Id)
            .Take(BatchSize)
            .Select(movie => new MovieProjection(
                movie.Id,
                movie.Title,
                movie.ReleaseDate,
                movie.OriginCountry
            ))
            .ToListAsync();

        if (rows.Count == 0)
            return null;

        foreach (MovieProjection row in rows)
            await _animeEnrichmentService!.EnrichMovieAsync(
                row.Id,
                row.Title,
                row.ReleaseDate.ParseYear(),
                row.OriginCountry is not null ? [row.OriginCountry] : null,
                false
            );

        await AnimeEnrichmentBackfillState.SetCursorAsync(
            appDb,
            "movie",
            rows[^1].Id,
            CancellationToken.None
        );
        return rows[^1].Id;
    }

    private sealed record TvProjection(
        int Id,
        string Title,
        DateTime? FirstAirDate,
        string? OriginCountry
    );

    private sealed record MovieProjection(
        int Id,
        string Title,
        DateTime? ReleaseDate,
        string? OriginCountry
    );
}
