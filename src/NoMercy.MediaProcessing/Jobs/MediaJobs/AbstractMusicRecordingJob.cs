// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database.Models;
using NoMercy.Encoder.Format.Container;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public abstract class AbstractMusicRecordingJob : IShouldQueue
{
    public Ulid LibraryId { get; set; }
    public Guid Id { get; set; }
    public Folder Folder { get; set; } = null!;
    public MusicBrainzTrack FoundTrack { get; set; } = null!;
    public ProcessMusicFolderJob.FolderMetadata FolderMetaData { get; set; } = null!;
    public BaseContainer Container { get; set; } = null!;

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