// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractEncoderJob : IShouldQueue
{
    public string Id { get; set; } = string.Empty;
    public Ulid FolderId { get; set; }

    public Ulid LibraryId { get; set; }

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public string InputFile { get; set; } = string.Empty;

    public abstract Task Handle();

    public void Dispose()
    {
    }
}