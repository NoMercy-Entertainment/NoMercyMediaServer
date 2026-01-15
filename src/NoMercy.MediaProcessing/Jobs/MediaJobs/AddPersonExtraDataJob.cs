// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.MediaProcessing.People;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.People;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddPersonExtraDataJob : AbstractShowExtraDataJob<TmdbPersonAppends, string>
{
    public override string QueueName => "queue";
    public override int Priority => 1;

    /** Note: TmdbPersonAppends is a reduced set to improve performance. */
    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        PersonRepository personRepository = new(context);
        PersonManager personManager = new(personRepository, jobDispatcher);

        foreach (TmdbPersonAppends person in Storage)
        {
            await personManager.StoreTranslations(person);
            await personManager.StoreImages(person);
        }

        Logger.MovieDb($"Show {Name}: People: Translations and Images stored", LogEventLevel.Debug);
    }
}