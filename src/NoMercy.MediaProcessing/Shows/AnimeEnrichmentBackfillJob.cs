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
/// via the "extras" queue at the lowest priority, and re-checks the
/// completion flag itself so a redundant dispatch is a no-op.
/// </summary>
[Serializable]
public class AnimeEnrichmentBackfillJob : IShouldQueue, IJobStorageInjector
{
    public string QueueName => "extras";

    // Lowest priority so live imports and on-demand classification always drain first.
    public int Priority => 0;

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

        bool tvProcessed = await ProcessTvBatchAsync(appDb, context);
        bool moviesProcessed = await ProcessMovieBatchAsync(appDb, context);

        if (!tvProcessed && !moviesProcessed)
        {
            await AnimeEnrichmentBackfillState.SetCompleteAsync(appDb, CancellationToken.None);
            return;
        }

        QueueRunner.Current?.Dispatcher.Dispatch(new AnimeEnrichmentBackfillJob(), "extras", 0);
    }

    private async Task<bool> ProcessTvBatchAsync(AppDbContext appDb, MediaContext context)
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
            return false;

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
        return true;
    }

    private async Task<bool> ProcessMovieBatchAsync(AppDbContext appDb, MediaContext context)
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
            return false;

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
        return true;
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
