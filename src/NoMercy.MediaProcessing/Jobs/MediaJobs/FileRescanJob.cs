// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class FileRescanJob : AbstractMediaJob
{
    public override string QueueName => "file";
    public override int Priority => 10;

    public override async Task Handle()
    {
        Logger.App($"[FileRescanJob] Handle() entered for id={Id}, libraryId={LibraryId}");

        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        LibraryRepository libraryRepository = new(context, StorageDriver);
        LibraryManager libraryManager = new(
            libraryRepository,
            jobDispatcher,
            context,
            StorageDriver,
            StorageFactory
        );

        Library? library = await libraryManager.RescanFiles(LibraryId, Id);

        string type = library?.Type switch
        {
            Config.TvMediaType or Config.AnimeMediaType => "tv",
            Config.MovieMediaType => "movie",
            _ => "unknown",
        };

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = [type, Id.ToString()] }
            );

            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = ["libraries", LibraryId.ToString()] }
            );

            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = ["home"] }
            );

            await EventBusProvider.Current.PublishAsync(
                new MediaFilesScannedEvent { MediaId = Id, LibraryId = LibraryId }
            );
        }
    }
}
