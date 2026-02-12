// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercy.Queue;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractReleaseJob : IShouldQueue
{
    public Guid Id { get; set; }
    public MediaFolderExtend MediaFolder { get; set; } = new();
    public Folder BaseFolder { get; set; } = new();
    public Ulid LibraryId { get; set; }

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void Dispose()
    {
    }
}