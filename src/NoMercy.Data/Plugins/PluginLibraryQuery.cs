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
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Data.Plugins;

/// <summary>
/// The library, translated for plugins.
/// <para>
/// This class is the reason plugins do not get the EF context. Every method
/// projects into a DTO owned by the plugin contract, so the schema underneath
/// stays free to change: a migration that renames a column is a change here and
/// nowhere else, instead of a break for every installed plugin. That is the
/// "never break a self-hosted user" rule reaching the plugin surface.
/// </para>
/// <para>Read-only throughout, and every query is <c>AsNoTracking</c>.</para>
/// </summary>
public class PluginLibraryQuery(IDbContextFactory<MediaContext> contextFactory)
    : IPluginLibraryQuery
{
    public async Task<IReadOnlyList<PluginLibrary>> GetLibrariesAsync(
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        return await context
            .Libraries.AsNoTracking()
            .Select(library => new PluginLibrary(
                library.Id.ToString(),
                library.Title,
                library.Type
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PluginLibraryShow>> GetShowsAsync(
        string? libraryId = null,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        IQueryable<Tv> shows = context.Tvs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(libraryId) && Ulid.TryParse(libraryId, out Ulid parsed))
            shows = shows.Where(show => show.LibraryId == parsed);

        // Flat projection then shape in memory, because the status has to be mapped out of
        // the provider's own wording and a switch over strings inside the projection turns
        // into a CASE expression in SQL for no gain.
        var rows = await shows
            .Select(show => new
            {
                show.Id,
                show.Title,
                Year = show.FirstAirDate == null ? (int?)null : show.FirstAirDate!.Value.Year,
                show.LibraryId,
                show.Folder,
                show.NumberOfEpisodes,
                show.HaveEpisodes,
                show.Status,
            })
            .ToListAsync(ct);

        return rows.Select(row => new PluginLibraryShow(
                row.Id,
                row.Title,
                row.Year,
                row.LibraryId.ToString(),
                row.Folder,
                row.NumberOfEpisodes,
                row.HaveEpisodes ?? 0
            )
            {
                Status = ShowStatus(row.Status),
            })
            .ToList();
    }

    /// <summary>
    /// The metadata provider's word for where a show stands, as this contract's own.
    /// <para>
    /// Anything unrecognised becomes <see cref="PluginShowStatus.Unknown"/> rather than a
    /// guess. A provider that adds a status this mapping has not heard of should leave a
    /// plugin saying "I do not know", which every plugin has to handle anyway, rather than
    /// quietly reading as "finished" — that is the reading that stops a plugin working on
    /// a series nobody cancelled.
    /// </para>
    /// </summary>
    private static PluginShowStatus ShowStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "returning series" => PluginShowStatus.Returning,
            "planned" => PluginShowStatus.Planned,
            "in production" => PluginShowStatus.InProduction,
            "pilot" => PluginShowStatus.Pilot,
            "ended" => PluginShowStatus.Ended,
            "canceled" or "cancelled" => PluginShowStatus.Canceled,
            _ => PluginShowStatus.Unknown,
        };

    public async Task<IReadOnlyList<PluginLibraryMovie>> GetMoviesAsync(
        string? libraryId = null,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        IQueryable<Movie> movies = context.Movies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(libraryId) && Ulid.TryParse(libraryId, out Ulid parsed))
            movies = movies.Where(movie => movie.LibraryId == parsed);

        return await movies
            .Select(movie => new PluginLibraryMovie(
                movie.Id,
                movie.Title,
                movie.ReleaseDate == null ? null : movie.ReleaseDate!.Value.Year,
                movie.LibraryId.ToString(),
                movie.Folder,
                movie.VideoFiles.Any()
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PluginLibraryEpisode>> GetEpisodesAsync(
        int showId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        // Every episode, including the ones with no file. A plugin working out
        // what is missing is looking for exactly those, so omitting them would
        // make the contract useless for its main purpose.
        return await context
            .Episodes.AsNoTracking()
            .Where(episode => episode.TvId == showId)
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .Select(episode => new PluginLibraryEpisode(
                episode.TvId,
                episode.SeasonNumber,
                episode.EpisodeNumber,
                episode.Title,
                episode.AirDate,
                episode.VideoFiles.Any()
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PluginLibraryFile>> GetShowFilesAsync(
        int showId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        // Flat projection then shape in memory: a GroupBy/First projection is
        // the SQLite APPLY trap this codebase avoids everywhere else.
        var rows = await context
            .VideoFiles.AsNoTracking()
            .Where(file => file.Episode != null && file.Episode.TvId == showId)
            .Select(file => new
            {
                file.Filename,
                file.Folder,
                file.HostFolder,
                file.Quality,
                SeasonNumber = file.Episode!.SeasonNumber,
                EpisodeNumber = file.Episode!.EpisodeNumber,
            })
            .ToListAsync(ct);

        return rows.Select(row => new PluginLibraryFile(
                showId,
                row.SeasonNumber,
                row.EpisodeNumber,
                string.Concat(row.HostFolder, row.Folder ?? string.Empty, row.Filename),
                row.Quality
            ))
            .ToList();
    }
}
