// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Movies;
using NoMercy.Networking.Dto;
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddCollectionExtraDataJob : AbstractMediaExraDataJob<TmdbCollectionAppends>
{
    public override string QueueName => "queue";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(movieRepository, jobDispatcher);

        CollectionRepository collectionRepository = new(context);
        CollectionManager collectionManager = new(collectionRepository, movieManager, jobDispatcher);

        await collectionManager.StoreImages(Storage);

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["collection", Storage.Id.ToString()]
        });
    }
}