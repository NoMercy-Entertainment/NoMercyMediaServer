// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class MovieExtrasJob : AbstractMediaExraDataJob<TmdbMovieAppends>
{
    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        IStorageBackend storageBackend = StorageProvider.Backend;
        IStorageFactory storageFactory = new StorageFactory(
            storageBackend,
            NullLogger<StorageFactory>.Instance
        );

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(
            movieRepository,
            jobDispatcher,
            storageFactory,
            storageBackend
        );

        PersonRepository personRepository = new(context);
        PersonManager personManager = new(personRepository, jobDispatcher);

        await personManager.Store(Storage);

        await movieManager.StoreImages(Storage);
        await movieManager.StoreSimilar(Storage);
        await movieManager.StoreRecommendations(Storage);
        await movieManager.StoreAlternativeTitles(Storage);
        await movieManager.StoreWatchProviders(Storage);
        await movieManager.StoreVideos(Storage);
        await movieManager.StoreCompanies(Storage);
        await movieManager.StoreKeywords(Storage);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = ["base", "info", Storage.Id.ToString()] }
            );
    }
}
