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
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MovieFileLibrary;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Events;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FFProbe;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Resolves a parsed video filename to a TMDB movie/episode match (slice 16.6).
/// Extracted from FileRepository so file enumeration no longer depends on TMDB.
/// A null return means "unidentified" — callers must NOT treat that as "drop".
/// </summary>
public class MediaIdentificationService(MediaContext context) : IMediaIdentificationService
{
    public async Task<(MovieOrEpisode match, string? imdbId)?> IdentifyAsync(
        MovieFile parsed,
        string libraryType,
        TimeSpan? duration,
        int? overrideTmdbId,
        bool seasonExplicit,
        CancellationToken ct = default
    )
    {
        return libraryType switch
        {
            MediaTypes.AnimeMediaType or MediaTypes.TvMediaType => await ResolveShowEpisodeAsync(
                context,
                libraryType,
                parsed,
                duration,
                overrideTmdbId,
                seasonExplicit
            ),
            MediaTypes.MovieMediaType => await ResolveMovieMatchAsync(
                context,
                libraryType,
                parsed,
                duration,
                overrideTmdbId
            ),
            _ => null,
        };
    }

    private static async Task<(MovieOrEpisode match, string? imdbId)?> ResolveShowEpisodeAsync(
        MediaContext ctx,
        string libraryType,
        MovieFile parsed,
        TimeSpan? duration,
        int? overrideTmdbId,
        bool seasonExplicit = false
    )
    {
        TmdbSearchClient searchClient = new();

        TmdbTvShow? show;
        TmdbPaginatedResponse<TmdbTvShow>? shows = null;

        if (overrideTmdbId.HasValue)
        {
            // Resolve directly by TMDB ID — no text search, no ambiguity
            TmdbTvClient overrideTvClient = new(overrideTmdbId.Value);
            TmdbTvShowDetails? overrideDetails = await overrideTvClient.Details(true);
            if (overrideDetails == null)
                return null;
            show = overrideDetails; // TmdbTvShowDetails : TmdbTvShow
        }
        else
        {
            shows = await searchClient.TvShow(parsed.Title.OrEmpty(), parsed.Year.OrEmpty(), true);
            show = shows?.Results.FirstOrDefault();
        }

        if (show == null || !parsed.Season.HasValue || !parsed.Episode.HasValue)
            return null;

        Ulid libraryId = await ctx
            .Libraries.Where(item => item.Type == libraryType)
            .Select(item => item.Id)
            .FirstOrDefaultAsync();

        await EnsureShowInLibraryAsync(ctx, show.Id, show.Name, libraryId);

        Episode? episode = ctx
            .Episodes.Where(item => item.TvId == show.Id)
            .Where(item => item.SeasonNumber == parsed.Season)
            .FirstOrDefault(item => item.EpisodeNumber == parsed.Episode);

        // When the season was explicit in the filename (e.g. S02E19), try the TMDB API first,
        // then episode groups (e.g. Crunchyroll splits seasons differently from TMDB default).
        if (episode == null && seasonExplicit)
        {
            TmdbEpisodeClient episodeClient = new(
                show.Id,
                parsed.Season.Value,
                parsed.Episode.Value
            );
            TmdbEpisodeDetails? details = await episodeClient.Details(true);

            // TMDB default doesn't have this season — try episode groups for alternate season splits
            if (details == null)
                episode = await ResolveSeasonedEpisodeFromGroupsAsync(
                    ctx,
                    show.Id,
                    parsed.Season.Value,
                    parsed.Episode.Value
                );

            if (details != null && episode == null)
            {
                Season? season = await ctx.Seasons.FirstOrDefaultAsync(s =>
                    s.TvId == show.Id && s.SeasonNumber == details.SeasonNumber
                );

                episode = new()
                {
                    Id = details.Id,
                    TvId = show.Id,
                    SeasonNumber = details.SeasonNumber,
                    EpisodeNumber = details.EpisodeNumber,
                    Title = details.Name,
                    Overview = details.Overview,
                    Still = details.StillPath,
                    VoteAverage = details.VoteAverage,
                    VoteCount = details.VoteCount,
                    AirDate = details.AirDate,
                    SeasonId = season?.Id ?? 0,
                };

                ctx.Episodes.Add(episode);
                await ctx.SaveChangesAsync();
            }
        }

        if (episode == null)
        {
            List<Episode> episodes = ctx
                .Episodes.Where(item => item.TvId == show.Id)
                .Where(item => item.SeasonNumber > 0)
                .OrderBy(item => item.SeasonNumber)
                .ThenBy(item => item.EpisodeNumber)
                .ToList();

            episode = episodes.ElementAtOrDefault(parsed.Episode.Value - 1);
        }

        if (episode == null)
            episode = await ResolveAbsoluteEpisodeAsync(ctx, show.Id, parsed.Episode.Value);

        // Try alternate search results for absolute-order anime (e.g. TMDB ranks live-action above anime)
        if (episode == null && shows!.Results.Count > 1)
        {
            foreach (TmdbTvShow altShow in shows.Results.Skip(1).Take(4))
            {
                TmdbTvClient altTvClient = new(altShow.Id);
                TmdbTvEpisodeGroups? altGroups = await altTvClient.EpisodeGroups(true);
                if (altGroups?.Results.Any(g => g.Type == 2) != true)
                    continue;

                await EnsureShowInLibraryAsync(ctx, altShow.Id, altShow.Name, libraryId);
                episode = await ResolveAbsoluteEpisodeAsync(ctx, altShow.Id, parsed.Episode.Value);
                if (episode != null)
                    break;
            }
        }

        if (episode == null)
        {
            TmdbEpisodeClient episodeClient = new(
                show.Id,
                parsed.Season.Value,
                parsed.Episode.Value
            );
            TmdbEpisodeDetails? details = await episodeClient.Details(true);
            if (details == null)
                return null;

            Season? season = await ctx.Seasons.FirstOrDefaultAsync(s =>
                s.TvId == show.Id && s.SeasonNumber == details.SeasonNumber
            );

            episode = new()
            {
                Id = details.Id,
                TvId = show.Id,
                SeasonNumber = details.SeasonNumber,
                EpisodeNumber = details.EpisodeNumber,
                Title = details.Name,
                Overview = details.Overview,
                Still = details.StillPath,
                VoteAverage = details.VoteAverage,
                VoteCount = details.VoteCount,
                AirDate = details.AirDate,
                SeasonId = season?.Id ?? 0,
            };

            ctx.Episodes.Add(episode);
            await ctx.SaveChangesAsync();
        }

        // Prefer the DB row over the search-side TmdbTvShow.Name — the local
        // Tv table is the source of truth for show metadata after a scan, and
        // a freshly added show may have been written by EnsureShowInLibraryAsync
        // above.
        string? showName =
            await ctx.Tvs.Where(t => t.Id == show.Id).Select(t => t.Title).FirstOrDefaultAsync()
            ?? show.Name;

        MovieOrEpisode match = new()
        {
            Id = episode.Id,
            Title = episode.Title.OrEmpty(),
            ShowName = showName,
            EpisodeNumber = episode.EpisodeNumber,
            SeasonNumber = episode.SeasonNumber,
            Still = episode.Still,
            Duration = duration,
            Overview = episode.Overview,
        };

        return (match, episode.ImdbId);
    }

    private static async Task<(MovieOrEpisode match, string? imdbId)?> ResolveMovieMatchAsync(
        MediaContext ctx,
        string libraryType,
        MovieFile parsed,
        TimeSpan? duration,
        int? overrideTmdbId
    )
    {
        TmdbMovie? movie;

        if (overrideTmdbId.HasValue)
        {
            // Resolve directly by TMDB ID — no text search, no ambiguity
            movie = new() { Id = overrideTmdbId.Value };
        }
        else
        {
            TmdbSearchClient searchClient = new();
            TmdbPaginatedResponse<TmdbMovie>? movies = await searchClient.Movie(
                parsed.Title.OrEmpty(),
                parsed.Year.OrEmpty(),
                true
            );
            movie = movies?.Results.FirstOrDefault();
        }

        if (movie == null)
            return null;

        Movie? movieItem = ctx.Movies.FirstOrDefault(item => item.Id == movie.Id);

        if (movieItem == null)
        {
            TmdbMovieClient movieClient = new(movie.Id);
            TmdbMovieDetails? details = await movieClient.Details(true);
            if (details == null)
                return null;

            bool hasMovie = ctx.Movies.Any(item => item.Id == movie.Id);

            Ulid libraryId = await ctx
                .Libraries.Where(item => item.Type == libraryType)
                .Select(item => item.Id)
                .FirstOrDefaultAsync();

            if (!hasMovie)
            {
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new UserNotifiedEvent
                        {
                            Title = "Movie not found",
                            Message = $"Movie {movie.Title} not found in library, adding now",
                            Type = "info",
                        }
                    );
                }
                MovieImportJob job = new() { LibraryId = libraryId, Id = movie.Id };
                await job.Handle();
            }

            movieItem = new()
            {
                Id = details.Id,
                Title = details.Title,
                Overview = details.Overview,
                Poster = details.PosterPath,
            };
        }

        MovieOrEpisode match = new()
        {
            Id = movieItem.Id,
            Title = movieItem.Title,
            Still = movieItem.Poster,
            Duration = duration,
            Overview = movieItem.Overview,
        };

        return (match, movieItem.ImdbId);
    }

    private static async Task EnsureShowInLibraryAsync(
        MediaContext ctx,
        int showId,
        string showName,
        Ulid libraryId
    )
    {
        bool hasShow = ctx.Tvs.Any(item => item.Id == showId);
        if (hasShow)
            return;

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new UserNotifiedEvent
                {
                    Title = "Show not found",
                    Message = $"Show {showName} not found in library, adding now",
                    Type = "info",
                }
            );
        }

        ShowImportJob job = new()
        {
            LibraryId = libraryId,
            Id = showId,
            HighPriority = true,
        };
        await job.Handle();
    }

    private static async Task<Episode?> ResolveAbsoluteEpisodeAsync(
        MediaContext ctx,
        int showId,
        int absoluteEpisodeNumber
    )
    {
        TmdbTvClient tvClient = new(showId);
        TmdbTvEpisodeGroups? episodeGroups = await tvClient.EpisodeGroups(true);
        if (episodeGroups == null)
        {
            Logger.App($"No episode groups found for show {showId}", LogEventLevel.Debug);
            return null;
        }

        // Try all "Absolute" type groups (type 2) — some shows have multiple
        TmdbEpisodeGroupsResult[] absoluteGroups = episodeGroups
            .Results.Where(g => g.Type == 2)
            .ToArray();

        if (absoluteGroups.Length == 0)
        {
            Logger.App(
                $"No absolute episode group (type 2) for show {showId}, available types: {string.Join(", ", episodeGroups.Results.Select(g => $"{g.Name}={g.Type}"))}",
                LogEventLevel.Debug
            );
            return null;
        }

        foreach (TmdbEpisodeGroupsResult absoluteGroup in absoluteGroups)
        {
            TmdbEpisodeGroupClient groupClient = new(absoluteGroup.Id);
            TmdbEpisodeGroupDetails? groupDetails = await groupClient.Details(true);
            if (groupDetails == null)
            {
                Logger.App(
                    $"Failed to fetch episode group details for {absoluteGroup.Id} ({absoluteGroup.Name})",
                    LogEventLevel.Debug
                );
                continue;
            }

            // Flatten all episodes across all groups, ordered by group order
            List<TmdbEpisodeGroupEpisode> allEpisodes = groupDetails
                .Groups.OrderBy(g => g.Order)
                .SelectMany(g => g.Episodes)
                .ToList();

            if (absoluteEpisodeNumber < 1 || absoluteEpisodeNumber > allEpisodes.Count)
            {
                Logger.App(
                    $"Absolute episode {absoluteEpisodeNumber} out of range in '{absoluteGroup.Name}' (has {allEpisodes.Count} episodes)",
                    LogEventLevel.Debug
                );
                continue;
            }

            TmdbEpisodeGroupEpisode target = allEpisodes[absoluteEpisodeNumber - 1];
            Logger.App(
                $"Resolved absolute episode {absoluteEpisodeNumber} → S{target.SeasonNumber:D2}E{target.EpisodeNumber:D2} ({target.Name}) via '{absoluteGroup.Name}'"
            );

            // Look up the resolved episode in the DB
            Episode? episode = await ctx.Episodes.FirstOrDefaultAsync(e =>
                e.TvId == showId
                && e.SeasonNumber == target.SeasonNumber
                && e.EpisodeNumber == target.EpisodeNumber
            );

            if (episode != null)
                return episode;

            // Fetch from TMDB and add to DB
            TmdbEpisodeClient episodeClient = new(
                showId,
                target.SeasonNumber,
                target.EpisodeNumber
            );
            TmdbEpisodeDetails? details = await episodeClient.Details(true);
            if (details == null)
                continue;

            Season? season = await ctx.Seasons.FirstOrDefaultAsync(s =>
                s.TvId == showId && s.SeasonNumber == details.SeasonNumber
            );

            episode = new()
            {
                Id = details.Id,
                TvId = showId,
                SeasonNumber = details.SeasonNumber,
                EpisodeNumber = details.EpisodeNumber,
                Title = details.Name,
                Overview = details.Overview,
                Still = details.StillPath,
                VoteAverage = details.VoteAverage,
                VoteCount = details.VoteCount,
                AirDate = details.AirDate,
                SeasonId = season?.Id ?? 0,
            };

            ctx.Episodes.Add(episode);
            await ctx.SaveChangesAsync();

            return episode;
        }

        return null;
    }

    /// <summary>
    /// Resolves an episode using TMDB episode groups when the default season structure doesn't match.
    /// E.g. Crunchyroll splits a show into S01/S02 but TMDB has it as a single season.
    /// Searches all episode group types for a group whose season/order matches the parsed season,
    /// then finds the episode by number within that group.
    /// </summary>
    private static async Task<Episode?> ResolveSeasonedEpisodeFromGroupsAsync(
        MediaContext ctx,
        int showId,
        int seasonNumber,
        int episodeNumber
    )
    {
        TmdbTvClient tvClient = new(showId);
        TmdbTvEpisodeGroups? episodeGroups = await tvClient.EpisodeGroups(true);
        if (episodeGroups?.Results is not { Length: > 0 })
            return null;

        // Prefer episode groups with the fewest sub-groups that still cover the target season.
        // E.g. for Season 2, a 2-group set (S1+S2) is better than a 3-group set (Specials+S1+S2).
        IEnumerable<TmdbEpisodeGroupsResult> sortedResults = episodeGroups
            .Results.Where(g => g.GroupCount >= seasonNumber)
            .OrderBy(g => g.GroupCount);

        foreach (TmdbEpisodeGroupsResult groupResult in sortedResults)
        {
            TmdbEpisodeGroupClient groupClient = new(groupResult.Id);
            TmdbEpisodeGroupDetails? groupDetails = await groupClient.Details(true);
            if (groupDetails == null)
                continue;

            // Groups within an episode group represent seasons/parts. Order values vary
            // (some 0-based, some 1-based), so sort by Order and use positional index.
            // Skip groups with no episodes (e.g. empty specials groups).
            List<TmdbEpisodeGroup> sortedGroups = groupDetails
                .Groups.Where(g => g.Episodes.Length > 0)
                .OrderBy(g => g.Order)
                .ToList();

            TmdbEpisodeGroup? targetGroup =
                sortedGroups.Count >= seasonNumber ? sortedGroups[seasonNumber - 1] : null;

            if (targetGroup == null)
                continue;

            // Find the episode by position within this group. Order values are show-global
            // (e.g. 24-47 for Season 2), so use sorted index instead.
            TmdbEpisodeGroupEpisode? target = targetGroup
                .Episodes.OrderBy(e => e.Order)
                .ElementAtOrDefault(episodeNumber - 1);

            if (target == null)
                continue;

            Logger.App(
                $"Resolved S{seasonNumber:D2}E{episodeNumber:D2} → TMDB S{target.SeasonNumber:D2}E{target.EpisodeNumber:D2} ({target.Name}) via episode group '{groupResult.Name}'"
            );

            // Look up in DB first
            Episode? episode = await ctx.Episodes.FirstOrDefaultAsync(e =>
                e.TvId == showId
                && e.SeasonNumber == target.SeasonNumber
                && e.EpisodeNumber == target.EpisodeNumber
            );

            if (episode != null)
                return episode;

            // Fetch from TMDB and create
            TmdbEpisodeClient episodeClient = new(
                showId,
                target.SeasonNumber,
                target.EpisodeNumber
            );
            TmdbEpisodeDetails? details = await episodeClient.Details(true);
            if (details == null)
                continue;

            Season? season = await ctx.Seasons.FirstOrDefaultAsync(s =>
                s.TvId == showId && s.SeasonNumber == details.SeasonNumber
            );

            episode = new()
            {
                Id = details.Id,
                TvId = showId,
                SeasonNumber = details.SeasonNumber,
                EpisodeNumber = details.EpisodeNumber,
                Title = details.Name,
                Overview = details.Overview,
                Still = details.StillPath,
                VoteAverage = details.VoteAverage,
                VoteCount = details.VoteCount,
                AirDate = details.AirDate,
                SeasonId = season?.Id ?? 0,
            };

            ctx.Episodes.Add(episode);
            await ctx.SaveChangesAsync();

            return episode;
        }

        return null;
    }

    private static readonly List<string> PrevSearchQueries = [];
}
