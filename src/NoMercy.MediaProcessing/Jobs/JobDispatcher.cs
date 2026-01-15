using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Jobs;

public class JobDispatcher
{
    public void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string id, string inputFile)
        where TJob : AbstractEncoderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, FolderId = folderId, Id = id, InputFile = inputFile };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, Guid releaseId, string filePath)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, ReleaseId = releaseId, InputFolder = filePath };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string filePath)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, FolderId = folderId, InputFolder = filePath };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(int id, Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = libraryId };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { LibraryId = libraryId };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(int id, Library library, int? priority = null)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = library.Id };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, priority ?? job.Priority);
    }

    internal void DispatchJob<TJob, TChild>(TChild data)
        where TJob : AbstractMediaExraDataJob<TChild>, new()
    {
        TJob job = new() { Storage = data };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    internal void DispatchJob<TJob, TChild>(IEnumerable<TChild> data, string name)
        where TJob : AbstractShowExtraDataJob<TChild, string>, new()
    {
        TJob job = new() { Storage = data, Name = name };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(string baseFolderPath, Ulid libraryId)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { InputFolder = baseFolderPath, LibraryId = libraryId };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(Ulid libraryId, Guid id, Folder baseFolder, MediaFolderExtend mediaFolder)
        where TJob : AbstractReleaseJob, new()
    {
        TJob job = new() { LibraryId = libraryId, Id = id, BaseFolder = baseFolder, MediaFolder = mediaFolder };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(Guid id1, Guid? id2 = null, Guid? id3 = null)
        where TJob : AbstractFanArtDataJob, new()
    {
        TJob job = new() { Id1 = id1, Id2 = id2, Id3 = id3 };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(MusicBrainzReleaseGroup musicBrainzReleaseGroup)
        where TJob : MusicDescriptionJob, new()
    {
        TJob job = new() { MusicBrainzReleaseGroup = musicBrainzReleaseGroup };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(MusicBrainzArtist musicBrainzArtist)
        where TJob : MusicDescriptionJob, new()
    {
        TJob job = new() { MusicBrainzArtist = musicBrainzArtist };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(
        Guid id,
        Ulid folderId,
        FolderMetadata folderMetaData,
        MediaFile mediaFile,
        MusicBrainzTrack foundTrack,
        Ulid libraryId,
        string inputFolder,
        string inputFile
    )
        where TJob : EncodeMusicJob, new()
    {
        TJob job = new()
        {
            Id = id,
            FolderId = folderId,
            FoundTrack = foundTrack, FolderMetaData = folderMetaData,
            MediaFile = mediaFile, LibraryId = libraryId,
            InputFolder = inputFolder, InputFile = inputFile
        };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(Track track)
        where TJob : AbstractLyricJob, new()
    {
        TJob job = new() { Track = track };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }
}