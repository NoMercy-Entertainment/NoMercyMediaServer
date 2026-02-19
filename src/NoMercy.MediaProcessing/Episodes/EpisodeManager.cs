using System.Collections.Concurrent;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Episodes;

public class EpisodeManager(
    IEpisodeRepository episodeRepository,
    JobDispatcher jobDispatcher
) : BaseManager, IEpisodeManager
{
    public async Task<IEnumerable<Episode>> Add(TmdbTvShow show, TmdbSeasonAppends season, bool? priority = false)
    {
        IEnumerable<TmdbEpisodeAppends> episodeAppends = await Collect(show, season, priority);

        IEnumerable<Episode> episodes = episodeAppends
            .Select(episode => new Episode
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
        
        Logger.MovieDb($"Show {show.Name}: Season {season.SeasonNumber} Episodes stored", LogEventLevel.Debug);
        
        jobDispatcher.DispatchJob<EpisodeExtrasJob, TmdbEpisodeAppends>(episodeAppends, show.Name);

        return episodes;
    }

    private static async Task<List<TmdbEpisodeAppends>> Collect(
        TmdbTvShow show, TmdbSeasonAppends season, bool? priority = false)
    {
        ConcurrentBag<TmdbEpisodeAppends> episodeAppends = [];

        await Parallel.ForEachAsync(season.Episodes, Config.ParallelOptions, async (episode, _) =>
        {
            try
            {
                using TmdbEpisodeClient tmdbEpisodeClient = new(show.Id, episode.SeasonNumber, episode.EpisodeNumber);
                TmdbEpisodeAppends? seasonTask = await tmdbEpisodeClient.WithAllAppends(priority);
                if (seasonTask is null) return;

                episodeAppends.Add(seasonTask);
            }
            catch (Exception e)
            {
                Logger.MovieDb(e.Message, LogEventLevel.Error);
            }
        });

        return episodeAppends.ToList();
    }

    internal async Task StoreTranslations(string showName, TmdbEpisodeAppends episode)
    {
        List<Translation> translations = episode.Translations.Translations
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
                EpisodeId = episode.Id
            })
            .ToList();

        await episodeRepository.StoreEpisodeTranslations(translations);

        Logger.MovieDb(
            $"Show {showName}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: Translations stored");
    }

    internal async Task StoreImages(string showName, TmdbEpisodeAppends episode)
    {
        try
        {
            IEnumerable<Image> stills = episode.TmdbEpisodeImages.Stills
                .Select(image => new Image
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
                    Site = "https://image.tmdb.org/t/p/"
                })
                .ToList();

            await episodeRepository.StoreEpisodeImages(stills);
            
            Logger.MovieDb(
                $"Show {showName}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: Images stored",
                LogEventLevel.Debug);
        }
        catch (Exception e)
        {
            Logger.MovieDb(
                $"Show {showName}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: Error storing images: {e.Message}",
                LogEventLevel.Error);
        }
    }
}