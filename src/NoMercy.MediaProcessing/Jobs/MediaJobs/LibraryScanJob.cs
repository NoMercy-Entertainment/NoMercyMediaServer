﻿// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.Events;
using NoMercy.MediaProcessing.Libraries;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class LibraryScanJob : AbstractMediaJob
{
    public override string QueueName => "library";
    public override int Priority => 10;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        IEventBus? eventBus = EventBusProvider.IsConfigured ? EventBusProvider.Current : null;

        LibraryRepository libraryRepository = new(context, StorageDriver);
        LibraryManager libraryManager = new(
            libraryRepository,
            jobDispatcher,
            context,
            StorageDriver,
            StorageFactory,
            eventBus
        );

        await libraryManager.ProcessNewLibraryItems(LibraryId);
    }
}
