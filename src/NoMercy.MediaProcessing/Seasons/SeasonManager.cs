using System.Collections.Concurrent;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Seasons;

public class SeasonManager(
    ISeasonRepository seasonRepository,
    JobDispatcher jobDispatcher
) : BaseManager, ISeasonManager
{
    public async Task<IEnumerable<TmdbSeasonAppends>> StoreSeasonsAsync(TmdbTvShowAppends show, bool? priority = false)
    {
        ConcurrentBag<TmdbSeasonAppends> seasonAppends = [];

        await Parallel.ForEachAsync(show.Seasons, Config.ParallelOptions, async (season, _) =>
        {
            try
            {
                using TmdbSeasonClient tmdbSeasonClient = new(show.Id, season.SeasonNumber);
                TmdbSeasonAppends? seasonTask = await tmdbSeasonClient.WithAppends([
                    "changes",
                    "credits",
                    "external_ids",
                    "images",
                    "translations"
                ], priority);
                if (seasonTask is null) return;

                seasonAppends.Add(seasonTask);
            }
            catch (Exception e)
            {
                Logger.MovieDb(e.Message, LogEventLevel.Error);
            }
        });

        IEnumerable<Season> seasons = seasonAppends
            .Select(s => new Season
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
        Logger.MovieDb($"Show {show.Name}: Seasons stored", LogEventLevel.Debug);

        jobDispatcher.DispatchJob<AddSeasonExtraDataJob, TmdbSeasonAppends>(seasonAppends, show.Name);

        return seasonAppends;
    }

    public Task UpdateSeasonAsync(string showName, TmdbSeasonAppends season)
    {
        throw new NotImplementedException();
    }

    public async Task RemoveSeasonAsync(string showName, TmdbSeasonAppends season)
    {
        await seasonRepository.RemoveSeasonAsync(season.Id);
        Logger.MovieDb($"Show {showName}: Season {season.SeasonNumber}: Removed", LogEventLevel.Debug);
    }

    internal async Task StoreTranslations(string showName, TmdbSeasonAppends season)
    {
        IEnumerable<Translation> translations = season.Translations.Translations
            .Where(translation => translation.Data.Title != null || translation.Data.Overview != "")
            .Select(translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                SeasonId = season.Id
            });

        await seasonRepository.StoreTranslationsAsync(translations);
        Logger.MovieDb($"Show {showName}: Season {season.SeasonNumber}: Translations stored", LogEventLevel.Debug);
    }

    internal async Task StoreImages(string showName, TmdbSeasonAppends season)
    {
        IEnumerable<Image> posters = season.TmdbSeasonImages.Posters
            .Select(image => new Image
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
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToList();

        await seasonRepository.StoreImagesAsync(posters);
        Logger.MovieDb($"Show {showName}: Season {season.SeasonNumber}: Images stored", LogEventLevel.Debug);
}
}