// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.MediaProcessing.Libraries;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class RescanLibraryJob : AbstractMediaJob
{
    public override string QueueName => "queue";
    public override int Priority => 10;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        LibraryRepository libraryRepository = new(context);
        LibraryManager libraryManager = new(libraryRepository, jobDispatcher);

        await libraryManager.ProcessLibrary(LibraryId);
    }
}