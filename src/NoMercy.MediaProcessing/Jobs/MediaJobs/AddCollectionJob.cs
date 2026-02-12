// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Movies;
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddCollectionJob : AbstractMediaJob
{
    public override string QueueName => "queue";
    public override int Priority => 4;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(movieRepository, jobDispatcher);

        CollectionRepository collectionRepository = new(context);
        CollectionManager collectionManager = new(collectionRepository, movieManager, jobDispatcher);

        Library collectionLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();

        TmdbCollectionAppends? collectionAppends = await collectionManager.Add(Id, collectionLibrary);
        if (collectionAppends == null) return;

        await collectionManager.AddCollectionMovies(collectionAppends, collectionLibrary);

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["libraries", LibraryId.ToString()]
            });

            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["collection"]
            });

            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["collection", Id.ToString()]
            });
        }
    }
}