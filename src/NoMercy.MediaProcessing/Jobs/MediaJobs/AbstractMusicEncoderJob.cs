// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database.Models;
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
    public Guid Id { get; set; }
    public Folder Folder { get; set; } = null!;

    public EncoderProfile Profile { get; set; } = null!;
    public MusicBrainzTrack foundTrack { get; set; } = null!;
    public ProcessMusicFolderJob.FolderMetadata folderMetaData { get; set; } = null!;
    public MediaFile mediaFile { get; set; } = null!;

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