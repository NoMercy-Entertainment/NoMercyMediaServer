// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.Networking.Dto;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class RescanFilesJob : AbstractMediaJob
{
    public override string QueueName => "image";
    public override int Priority => 10;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        LibraryRepository libraryRepository = new(context);
        LibraryManager libraryManager = new(libraryRepository, jobDispatcher);
        
        await libraryManager.RescanFiles(LibraryId, Id);
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["base","info", Id.ToString()]
        });

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["libraries", LibraryId.ToString()]
        });

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["home"]
        });
        
        // Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        // {
        //     QueryKey = ["base","collection", movieAppends.BelongsToCollection?.Id.ToString()]
        // });
    }
}