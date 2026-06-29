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
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Seasons;

public class SeasonManager(
    ISeasonRepository seasonRepository,
    JobDispatcher jobDispatcher,
    ILogger<SeasonManager> logger
) : BaseManager, ISeasonManager
{
    public async Task<IEnumerable<TmdbSeasonAppends>> StoreSeasonsAsync(
        TmdbTvShowAppends show,
        bool? priority = false
    )
    {
        ConcurrentBag<TmdbSeasonAppends> seasonAppends = [];

        await Parallel.ForEachAsync(
            show.Seasons,
            SystemParallelism.Options,
            async (season, _) =>
            {
                try
                {
                    using TmdbSeasonClient tmdbSeasonClient = new(show.Id, season.SeasonNumber);
                    TmdbSeasonAppends? seasonTask = await tmdbSeasonClient.WithAppends(
                        ["changes", "credits", "external_ids", "images", "translations"],
                        priority
                    );
                    if (seasonTask is null)
                        return;

                    seasonAppends.Add(seasonTask);
                }
                catch (Exception e)
                {
                    logger.LogError(e.Message);
                }
            }
        );

        IEnumerable<Season> seasons = seasonAppends.Select(s => new Season
        {
            Id = s.Id,
            Title = s.Name,
            AirDate = s.AirDate,
            EpisodeCount = s.Episodes.Length,
            Overview = s.Overview,
            Poster = s.PosterPath,
            SeasonNumber = s.SeasonNumber,
            TvId = show.Id,
        });

        await seasonRepository.StoreAsync(seasons);
        logger.LogDebug("Show {Name}: Seasons stored", show.Name);

        foreach (Season season in seasons)
            jobDispatcher.DispatchColorPaletteJob("season", season.Id.ToString());

        jobDispatcher.DispatchJob<SeasonExtrasJob, TmdbSeasonAppends>(seasonAppends, show.Name);

        return seasonAppends;
    }

    public Task UpdateSeasonAsync(string showName, TmdbSeasonAppends season)
    {
        // Refresh the existing season's metadata in place; the show link (TvId)
        // is left untouched because a season update never re-parents a season.
        return seasonRepository.UpdateAsync(
            new Season
            {
                Id = season.Id,
                Title = season.Name,
                AirDate = season.AirDate,
                EpisodeCount = season.Episodes.Length,
                Overview = season.Overview,
                Poster = season.PosterPath,
                SeasonNumber = season.SeasonNumber,
            }
        );
    }

    public async Task RemoveSeasonAsync(string showName, TmdbSeasonAppends season)
    {
        await seasonRepository.RemoveSeasonAsync(season.Id);
        logger.LogDebug(
            "Show {ShowName}: Season {SeasonNumber}: Removed",
            showName,
            season.SeasonNumber
        );
    }

    internal async Task StoreTranslations(string showName, TmdbSeasonAppends season)
    {
        IEnumerable<Translation> translations = season
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
                SeasonId = season.Id,
            });

        await seasonRepository.StoreTranslationsAsync(translations);
        logger.LogDebug(
            "Show {ShowName}: Season {SeasonNumber}: Translations stored",
            showName,
            season.SeasonNumber
        );
    }

    internal async Task StoreImages(string showName, TmdbSeasonAppends season)
    {
        IEnumerable<Image> posters = season
            .TmdbSeasonImages.Posters.Select(image => new Image
            {
                AspectRatio = image.AspectRatio,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                FilePath = image.FilePath,
                Width = image.Width,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                SeasonId = season.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToList();

        await seasonRepository.StoreImagesAsync(posters);
        logger.LogDebug(
            "Show {ShowName}: Season {SeasonNumber}: Images stored",
            showName,
            season.SeasonNumber
        );

        await using MediaContext db = new();
        List<int> imageIds = await db
            .Images.AsNoTracking()
            .Where(i =>
                i.SeasonId == season.Id && (i._colorPalette == null || i._colorPalette == "")
            )
            .Select(i => i.Id)
            .ToListAsync();

        foreach (int id in imageIds)
            jobDispatcher.DispatchColorPaletteJob("image", id.ToString());
    }
}
