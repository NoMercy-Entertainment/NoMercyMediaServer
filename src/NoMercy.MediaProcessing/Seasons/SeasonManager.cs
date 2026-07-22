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
            source: show.Seasons,
            parallelOptions: SystemParallelism.Options,
            body: async (season, _) =>
            {
                try
                {
                    using TmdbSeasonClient tmdbSeasonClient = new(tvId: show.Id, seasonNumber: season.SeasonNumber);
                    TmdbSeasonAppends? seasonTask = await tmdbSeasonClient.WithAppends(
                        appendices: ["changes", "credits", "external_ids", "images", "translations"],
                        priority: priority
                    );
                    if (seasonTask is null)
                        return;

                    seasonAppends.Add(item: seasonTask);
                }
                catch (Exception e)
                {
                    logger.LogError(message: e.Message);
                }
            }
        );

        IEnumerable<Season> seasons = seasonAppends.Select(selector: s => new Season
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

        await seasonRepository.StoreAsync(seasons: seasons);
        logger.LogDebug(message: "Show {Name}: Seasons stored", args: show.Name);

        foreach (Season season in seasons)
            jobDispatcher.DispatchColorPaletteJob(entityType: "season", entityId: season.Id.ToString());

        jobDispatcher.DispatchJob<SeasonExtrasJob, TmdbSeasonAppends>(data: seasonAppends, name: show.Name);

        return seasonAppends;
    }

    public Task UpdateSeasonAsync(string showName, TmdbSeasonAppends season)
    {
        // Refresh the existing season's metadata in place; the show link (TvId)
        // is left untouched because a season update never re-parents a season.
        return seasonRepository.UpdateAsync(
            season: new()
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
        await seasonRepository.RemoveSeasonAsync(seasonId: season.Id);
        logger.LogDebug(
            message: "Show {ShowName}: Season {SeasonNumber}: Removed", args: [showName, season.SeasonNumber]
        );
    }

    internal async Task StoreTranslations(string showName, TmdbSeasonAppends season)
    {
        IEnumerable<Translation> translations = season
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
                SeasonId = season.Id,
            });

        await seasonRepository.StoreTranslationsAsync(translations: translations);
        logger.LogDebug(
            message: "Show {ShowName}: Season {SeasonNumber}: Translations stored", args: [showName, season.SeasonNumber]
        );
    }

    internal async Task StoreImages(string showName, TmdbSeasonAppends season)
    {
        IEnumerable<Image> posters = season
            .TmdbSeasonImages.Posters.Select(selector: image => new Image
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

        await seasonRepository.StoreImagesAsync(images: posters);
        logger.LogDebug(
            message: "Show {ShowName}: Season {SeasonNumber}: Images stored", args: [showName, season.SeasonNumber]
        );

        await using MediaContext db = new();
        List<int> imageIds = await db
            .Images.AsNoTracking()
            .Where(predicate: i =>
                i.SeasonId == season.Id && (i._colorPalette == null || i._colorPalette == "")
            )
            .Select(selector: i => i.Id)
            .ToListAsync();

        foreach (int id in imageIds)
            jobDispatcher.DispatchColorPaletteJob(entityType: "image", entityId: id.ToString());
    }
}
