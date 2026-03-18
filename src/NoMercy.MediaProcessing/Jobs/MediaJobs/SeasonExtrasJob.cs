// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.MediaProcessing.People;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.Season;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class SeasonExtrasJob : AbstractShowExtraDataJob<TmdbSeasonAppends, string>
{
    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        SeasonRepository seasonRepository = new(context);
        SeasonManager seasonManager = new(seasonRepository, jobDispatcher);

        PersonRepository personRepository = new(context);
        PersonManager personManager = new(personRepository, jobDispatcher);

        foreach (TmdbSeasonAppends season in Storage)
        {
            await personManager.Store(season);

            await seasonManager.StoreImages(Name, season);
            await seasonManager.StoreTranslations(Name, season);
        }

        Logger.MovieDb($"Show {Name}: Seasons: Images and Translations stored", LogEventLevel.Verbose);
    }
}