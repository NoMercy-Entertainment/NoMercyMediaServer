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
        IEnumerable<TmdbEpisodeAppends> episodeAppends = await Collect(show, season, priority);

        IEnumerable<Episode> episodes = episodeAppends.Select(episode => new Episode
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
            "Show {Name}: Season {SeasonNumber} Episodes stored",
            show.Name,
            season.SeasonNumber
        );

        foreach (Episode episode in episodes)
            jobDispatcher.DispatchColorPaletteJob("episode", episode.Id.ToString());

        jobDispatcher.DispatchJob<EpisodeExtrasJob, TmdbEpisodeAppends>(episodeAppends, show.Name);

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
            season.Episodes,
            SystemParallelism.Options,
            async (episode, _) =>
            {
                try
                {
                    using TmdbEpisodeClient tmdbEpisodeClient = new(
                        show.Id,
                        episode.SeasonNumber,
                        episode.EpisodeNumber
                    );
                    TmdbEpisodeAppends? seasonTask = await tmdbEpisodeClient.WithAllAppends(
                        priority
                    );
                    if (seasonTask is null)
                        return;

                    episodeAppends.Add(seasonTask);
                }
                catch (Exception e)
                {
                    logger.LogError(e.Message);
                }
            }
        );

        return episodeAppends.ToList();
    }

    internal async Task StoreTranslations(string showName, TmdbEpisodeAppends episode)
    {
        List<Translation> translations = episode
            .Translations.Translations.Where(translation =>
                translation.Data.Title != null || translation.Data.Overview != ""
            )
            .Select(translation => new Translation
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

        await episodeRepository.StoreEpisodeTranslations(translations);

        logger.LogInformation(
            "Show {ShowName}: Season {SeasonNumber} Episode {EpisodeNumber}: Translations stored",
            showName,
            episode.SeasonNumber,
            episode.EpisodeNumber
        );
    }

    internal async Task StoreImages(string showName, TmdbEpisodeAppends episode)
    {
        try
        {
            IEnumerable<Image> stills = episode
                .TmdbEpisodeImages.Stills.Select(image => new Image
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

            await episodeRepository.StoreEpisodeImages(stills);

            logger.LogDebug(
                "Show {ShowName}: Season {SeasonNumber} Episode {EpisodeNumber}: Images stored",
                showName,
                episode.SeasonNumber,
                episode.EpisodeNumber
            );

            await using MediaContext db = new();
            List<int> imageIds = await db
                .Images.AsNoTracking()
                .Where(i =>
                    i.EpisodeId == episode.Id && (i._colorPalette == null || i._colorPalette == "")
                )
                .Select(i => i.Id)
                .ToListAsync();

            foreach (int id in imageIds)
                jobDispatcher.DispatchColorPaletteJob("image", id.ToString());
        }
        catch (Exception e)
        {
            logger.LogError(
                "Show {ShowName}: Season {SeasonNumber} Episode {EpisodeNumber}: Error storing images: {Message}",
                showName,
                episode.SeasonNumber,
                episode.EpisodeNumber,
                e.Message
            );
        }
    }
}
