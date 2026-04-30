// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.Providers.Helpers;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class LibraryRescanJob : AbstractMediaJob
{
    public override string QueueName => "library";
    public override int Priority => 10;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        IEventBus? eventBus = EventBusProvider.IsConfigured ? EventBusProvider.Current : null;

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
            storageFactory,
            eventBus
        );

        await libraryManager.ProcessLibrary(LibraryId);
    }
}
