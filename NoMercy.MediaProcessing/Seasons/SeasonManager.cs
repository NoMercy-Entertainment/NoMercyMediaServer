using System.Collections.Concurrent;
using System.Collections.Immutable;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem;
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
    public async Task<List<TmdbSeasonAppends>> StoreSeasonsAsync(TmdbTvShowAppends show)
    {
        List<TmdbSeasonAppends> seasonAppends = [];
        
        await Parallel.ForEachAsync(show.Seasons, async (season, _) =>
        {
            try
            {
                using TmdbSeasonClient tmdbSeasonClient = new(show.Id, season.SeasonNumber);
                TmdbSeasonAppends? seasonTask = await tmdbSeasonClient.WithAllAppends();
                if (seasonTask is null) return;
                
                seasonAppends.Add(seasonTask);
            }
            catch (Exception e)
            {
                Logger.MovieDb(e, LogEventLevel.Error);
            }
        });
        
        seasonAppends = seasonAppends
            .OrderBy(keySelector: f => f.SeasonNumber)
            .ToList();

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
                _colorPalette = MovieDbImage.ColorPalette("poster", s.PosterPath).Result
            });

        await seasonRepository.StoreAsync(seasons);
            
        Logger.MovieDb($"Show {show.Name}: Seasons stored");

         List<Task> promises = [];
         foreach (TmdbSeasonAppends season in seasonAppends)
         {
            promises.Add(StoreImagesAsync(show.Name, season));
            promises.Add(StoreTranslationsAsync(show.Name, season));
        }
        
        await Task.WhenAll(promises);

        return seasonAppends;
    }
    
    public Task UpdateSeasonAsync(string showName, TmdbSeasonAppends season)
    {
        throw new NotImplementedException();
    }
    
    public async Task RemoveSeasonAsync(string showName, TmdbSeasonAppends season)
    {
        await seasonRepository.RemoveSeasonAsync(season.Id);
        
        Logger.MovieDb($"Show {showName}, Season {season.SeasonNumber}: Removed");
    }
    
    internal async Task StoreTranslationsAsync(string showName, TmdbSeasonAppends season)
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

        Logger.MovieDb($"Show {showName}, Season {season.SeasonNumber}: Translations stored");
    }

    internal async Task StoreImagesAsync(string showName, TmdbSeasonAppends season)
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
         
         IEnumerable<Image> posterJobItems = posters
             .Select(x => new Image { FilePath = x.FilePath });
         jobDispatcher.DispatchJob<ImagePaletteJob, Image>(season.Id, posterJobItems);
         
         Logger.MovieDb($"Show {showName}, Season {season.SeasonNumber}: Images stored");
    }
}
