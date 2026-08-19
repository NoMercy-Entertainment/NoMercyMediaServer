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
/// Dispatched once, on boot, by <c>AnimeEnrichmentBackfillStartupService</c>
/// (mirrors <c>PaletteBackfillStartupService</c>'s one-shot pattern) via
/// the "extras" queue at the lowest priority, and re-checks the completion
/// flag itself so a redundant dispatch is a no-op.
/// </summary>
[Serializable]
public class AnimeEnrichmentBackfillJob : IShouldQueue, IJobStorageInjector
{
    public string QueueName => "extras";

    // Lowest priority so live imports and on-demand classification always drain first.
    public int Priority => 0;

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

        if (await AnimeEnrichmentBackfillState.IsCompleteAsync(CancellationToken.None))
            return;

        await using MediaContext context = await _contextFactory.CreateDbContextAsync();

        List<TvProjection> tvRows = await context
            .Tvs.AsNoTracking()
            .Where(tv => tv.Library.Type == "anime")
            .Select(tv => new TvProjection(tv.Id, tv.Title, tv.FirstAirDate, tv.OriginCountry))
            .ToListAsync();

        List<MovieProjection> movieRows = await context
            .Movies.AsNoTracking()
            .Where(movie => movie.Library.Type == "anime")
            .Select(movie => new MovieProjection(
                movie.Id,
                movie.Title,
                movie.ReleaseDate,
                movie.OriginCountry
            ))
            .ToListAsync();

        IEnumerable<(int TvId, string Title, int? Year, string[]? OriginCountry)> tvShows =
            tvRows.Select(row =>
                (
                    TvId: row.Id,
                    row.Title,
                    Year: (int?)row.FirstAirDate.ParseYear(),
                    OriginCountry: row.OriginCountry is not null
                        ? new[] { row.OriginCountry }
                        : null
                )
            );

        IEnumerable<(int MovieId, string Title, int? Year, string[]? OriginCountry)> movies =
            movieRows.Select(row =>
                (
                    MovieId: row.Id,
                    row.Title,
                    Year: (int?)row.ReleaseDate.ParseYear(),
                    OriginCountry: row.OriginCountry is not null
                        ? new[] { row.OriginCountry }
                        : null
                )
            );

        await RunAsync(tvShows);
        await RunMoviesAsync(movies);

        await AnimeEnrichmentBackfillState.SetCompleteAsync(CancellationToken.None);
    }

    public async Task RunAsync(
        IEnumerable<(int TvId, string Title, int? Year, string[]? OriginCountry)> tvShows
    )
    {
        if (_animeEnrichmentService is null)
            return;

        foreach ((int tvId, string title, int? year, string[]? originCountry) in tvShows)
            await _animeEnrichmentService.EnrichTvAsync(tvId, title, year, originCountry, false);
    }

    public async Task RunMoviesAsync(
        IEnumerable<(int MovieId, string Title, int? Year, string[]? OriginCountry)> movies
    )
    {
        if (_animeEnrichmentService is null)
            return;

        foreach ((int movieId, string title, int? year, string[]? originCountry) in movies)
            await _animeEnrichmentService.EnrichMovieAsync(
                movieId,
                title,
                year,
                originCountry,
                false
            );
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
