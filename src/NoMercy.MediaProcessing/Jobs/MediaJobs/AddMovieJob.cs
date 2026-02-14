// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Movies;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddMovieJob : AbstractMediaJob
{
    public override string QueueName => "queue";
    public override int Priority => 5;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        FileRepository fileRepository = new(context);
        FileManager fileManager = new(fileRepository);

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(movieRepository, jobDispatcher);

        Library movieLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();

        bool wasEmpty = !await context.LibraryMovie.AnyAsync(lm => lm.LibraryId == LibraryId);

        TmdbMovieAppends? movieAppends = await movieManager.Add(Id, movieLibrary);
        if (movieAppends == null) return;

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(new MediaAddedEvent
            {
                MediaId = Id,
                MediaType = "movie",
                Title = movieAppends.Title ?? $"Movie {Id}",
                LibraryId = LibraryId
            });
        }

        if (movieAppends.BelongsToCollection != null)
            jobDispatcher.DispatchJob<AddCollectionJob>(movieAppends.BelongsToCollection.Id, LibraryId);

        jobDispatcher.DispatchJob<RescanFilesJob>(Id, movieLibrary);

        Logger.App($"Movie {Id} added to library, extra data will be added in the background");
        
        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
            {
                QueryKey = ["base", "info", Id.ToString()]
            });

            if (wasEmpty)
                await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
                {
                    QueryKey = ["libraries"]
                });
        }
    }
}