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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;
using IJobDispatcher = NoMercy.MediaProcessing.Jobs.IJobDispatcher;

namespace NoMercy.MediaProcessing.Episodes;

public class EpisodeManager(
    IEpisodeRepository episodeRepository,
    IJobDispatcher jobDispatcher,
    ILogger<EpisodeManager> logger
) : BaseManager, IEpisodeManager
{
    public async Task<IEnumerable<Episode>> Add(
        TmdbTvShow show,
        TmdbSeasonAppends season,
        bool? priority = false
    )
    {
        IEnumerable<TmdbEpisodeAppends> episodeAppends = await Collect(show: show, season: season, priority: priority);

        IEnumerable<Episode> episodes = episodeAppends.Select(selector: episode => new Episode
        {
            TvId = show.Id,
            SeasonId = season.Id,

            Id = episode.Id,
            Title = episode.Name,
            AirDate = episode.AirDate,
            EpisodeNumber = episode.EpisodeNumber,
            ImdbId = episode.TmdbEpisodeExternalIds.ImdbId,
            Overview = episode.Overview,
            ProductionCode = episode.ProductionCode,
            SeasonNumber = episode.SeasonNumber,
            Still = episode.StillPath,
            TvdbId = episode.TmdbEpisodeExternalIds.TvdbId,
            VoteAverage = episode.VoteAverage,
            VoteCount = episode.VoteCount,
        });

        logger.LogDebug(
            message: "Show {Name}: Season {SeasonNumber} Episodes stored", args: [show.Name, season.SeasonNumber]
        );

        foreach (Episode episode in episodes)
            jobDispatcher.DispatchColorPaletteJob(entityType: "episode", entityId: episode.Id.ToString());

        jobDispatcher.DispatchJob<EpisodeExtrasJob, TmdbEpisodeAppends>(data: episodeAppends, name: show.Name);

        return episodes;
    }

    private async Task<List<TmdbEpisodeAppends>> Collect(
        TmdbTvShow show,
        TmdbSeasonAppends season,
        bool? priority = false
    )
    {
        ConcurrentBag<TmdbEpisodeAppends> episodeAppends = [];

        await Parallel.ForEachAsync(
            source: season.Episodes,
            parallelOptions: SystemParallelism.Options,
            body: async (episode, _) =>
            {
                try
                {
                    using TmdbEpisodeClient tmdbEpisodeClient = new(
                        id: show.Id,
                        seasonNumber: episode.SeasonNumber,
                        episodeNumber: episode.EpisodeNumber
                    );
                    TmdbEpisodeAppends? seasonTask = await tmdbEpisodeClient.WithAllAppends(
                        priority: priority
                    );
                    if (seasonTask is null)
                        return;

                    episodeAppends.Add(item: seasonTask);
                }
                catch (Exception e)
                {
                    logger.LogError(message: e.Message);
                }
            }
        );

        return episodeAppends.ToList();
    }

    internal async Task StoreTranslations(string showName, TmdbEpisodeAppends episode)
    {
        List<Translation> translations = episode
            .Translations.Translations.Where(predicate: translation =>
                translation.Data.Title != null || translation.Data.Overview != ""
            )
            .Select(selector: translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                EpisodeId = episode.Id,
            })
            .ToList();

        await episodeRepository.StoreEpisodeTranslations(translations: translations);

        logger.LogInformation(
            message: "Show {ShowName}: Season {SeasonNumber} Episode {EpisodeNumber}: Translations stored", args: [showName, episode.SeasonNumber, episode.EpisodeNumber]
        );
    }

    internal async Task StoreImages(string showName, TmdbEpisodeAppends episode)
    {
        try
        {
            IEnumerable<Image> stills = episode
                .TmdbEpisodeImages.Stills.Select(selector: image => new Image
                {
                    AspectRatio = image.AspectRatio,
                    FilePath = image.FilePath,
                    Height = image.Height,
                    Iso6391 = image.Iso6391,
                    VoteAverage = image.VoteAverage,
                    VoteCount = image.VoteCount,
                    Width = image.Width,
                    EpisodeId = episode.Id,
                    Type = "still",
                    Site = "https://image.tmdb.org/t/p/",
                })
                .ToList();

            await episodeRepository.StoreEpisodeImages(images: stills);

            logger.LogDebug(
                message: "Show {ShowName}: Season {SeasonNumber} Episode {EpisodeNumber}: Images stored", args: [showName, episode.SeasonNumber, episode.EpisodeNumber]
            );

            await using MediaContext db = new();
            List<int> imageIds = await db
                .Images.AsNoTracking()
                .Where(predicate: i =>
                    i.EpisodeId == episode.Id && (i._colorPalette == null || i._colorPalette == "")
                )
                .Select(selector: i => i.Id)
                .ToListAsync();

            foreach (int id in imageIds)
                jobDispatcher.DispatchColorPaletteJob(entityType: "image", entityId: id.ToString());
        }
        catch (Exception e)
        {
            logger.LogError(
                message: "Show {ShowName}: Season {SeasonNumber} Episode {EpisodeNumber}: Error storing images: {Message}", args: [showName, episode.SeasonNumber, episode.EpisodeNumber, e.Message]
            );
        }
    }
}
