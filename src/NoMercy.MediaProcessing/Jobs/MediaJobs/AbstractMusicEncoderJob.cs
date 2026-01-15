// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMusicEncoderJob : IShouldQueue
{
    public Ulid LibraryId { get; set; }
    public Guid Id { get; set; }

    public Ulid FolderId { get; set; }

    public MusicBrainzTrack FoundTrack { get; set; } = null!;
    public FolderMetadata FolderMetaData { get; set; } = null!;
    public MediaFile MediaFile { get; set; } = null!;

    public string InputFolder { get; set; } = string.Empty;

    public string InputFile { get; set; } = string.Empty;

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }
}