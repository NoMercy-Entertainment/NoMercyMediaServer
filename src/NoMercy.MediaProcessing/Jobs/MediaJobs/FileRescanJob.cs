// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.Providers.Helpers;
using NoMercy.Storage;

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
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        IStorageBackend storageBackend = StorageProvider.Backend;
        IStorageFactory storageFactory = new StorageFactory(
            storageBackend,
            NullLogger<StorageFactory>.Instance
        );

        LibraryRepository libraryRepository = new(context, storageBackend);
        LibraryManager libraryManager = new(
            libraryRepository,
            jobDispatcher,
            context,
            storageBackend,
            storageFactory
        );

        await libraryManager.RescanFiles(LibraryId, Id);

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = ["base", "info", Id.ToString()] }
            );

            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = ["libraries", LibraryId.ToString()] }
            );

            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent { QueryKey = ["home"] }
            );

            // Signal that VideoFile rows are now queryable — auto-encode
            // subscribers listen for this to dispatch encoding jobs against
            // folders that have EncoderProfileFolder assignments.
            await EventBusProvider.Current.PublishAsync(
                new MediaFilesScannedEvent { MediaId = Id, LibraryId = LibraryId }
            );
        }
    }
}
