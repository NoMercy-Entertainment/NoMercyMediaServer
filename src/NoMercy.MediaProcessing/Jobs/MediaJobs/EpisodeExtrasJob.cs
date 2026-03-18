// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.People;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.Episode;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class EpisodeExtrasJob : AbstractShowExtraDataJob<TmdbEpisodeAppends, string>
{
    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        EpisodeRepository episodeRepository = new(context);
        EpisodeManager episodeManager = new(episodeRepository, jobDispatcher);

        PersonRepository personRepository = new(context);
        PersonManager personManager = new(personRepository, jobDispatcher);
        
        foreach (TmdbEpisodeAppends episode in Storage)
        {
            await personManager.Store(episode);
            await episodeManager.StoreTranslations(Name, episode);
            await episodeManager.StoreImages(Name, episode);
        }

        Logger.MovieDb(
            $"Show {Name}: Season {Storage.FirstOrDefault()?.SeasonNumber} Episodes: Images and Translations stored",
            LogEventLevel.Debug);
    }
}