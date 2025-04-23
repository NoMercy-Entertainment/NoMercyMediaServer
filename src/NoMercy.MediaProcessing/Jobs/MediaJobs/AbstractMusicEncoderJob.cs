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
    public Ulid LibraryId { get; set; }
    public Guid Id { get; set; }
    public Folder Folder { get; set; } = null!;

    public EncoderProfile Profile { get; set; } = null!;
    public MusicBrainzTrack foundTrack { get; set; } = null!;
    public ProcessMusicFolderJob.FolderMetadata folderMetaData { get; set; } = null!;
    public MediaFile mediaFile { get; set; } = null!;
    
    public string  InputFolder { get; set; } = string.Empty;
    
    public string  InputFile { get; set; } = string.Empty;

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