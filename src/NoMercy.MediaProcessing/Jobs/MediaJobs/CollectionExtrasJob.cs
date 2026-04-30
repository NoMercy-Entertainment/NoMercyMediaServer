// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Movies;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class CollectionExtrasJob : AbstractMediaExraDataJob<TmdbCollectionAppends>
{
    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        IStorageBackend storageBackend = StorageProvider.Backend;
        IStorage storage = StorageProvider.Storage;

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(movieRepository, jobDispatcher, storage);

        CollectionRepository collectionRepository = new(context);
        CollectionManager collectionManager = new(
            collectionRepository,
            movieManager,
            jobDispatcher
        );

        await collectionManager.StoreImages(Storage);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = ["collection", Storage.Id.ToString()] }
            );
    }
}
