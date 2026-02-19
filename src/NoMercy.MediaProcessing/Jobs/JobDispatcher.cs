using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercyQueue;
using QueueJobDispatcher = NoMercyQueue.JobDispatcher;

namespace NoMercy.MediaProcessing.Jobs;

public class JobDispatcher
{
    private readonly QueueJobDispatcher? _dispatcher;

    public JobDispatcher(QueueJobDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public JobDispatcher()
    {
        _dispatcher = QueueRunner.Current?.Dispatcher;
    }

    private QueueJobDispatcher Dispatcher =>
        _dispatcher ?? throw new InvalidOperationException(
            "JobDispatcher requires a QueueRunner instance. Ensure QueueRunner is initialized before dispatching jobs.");

    public virtual void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string id, string inputFile)
        where TJob : AbstractEncoderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, FolderId = folderId, Id = id, InputFile = inputFile };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, Guid releaseId, string filePath)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, ReleaseId = releaseId, InputFolder = filePath };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string filePath)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, FolderId = folderId, InputFolder = filePath };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(int id, Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = libraryId };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { LibraryId = libraryId };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(int id, Library library, int? priority = null)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = library.Id };
        Dispatcher.Dispatch(job, job.QueueName, priority ?? job.Priority);
    }

    internal virtual void DispatchJob<TJob, TChild>(TChild data)
        where TJob : AbstractMediaExraDataJob<TChild>, new()
    {
        TJob job = new() { Storage = data };
        Dispatcher.Dispatch(job);
    }

    internal virtual void DispatchJob<TJob, TChild>(IEnumerable<TChild> data, string name)
        where TJob : AbstractShowExtraDataJob<TChild, string>, new()
    {
        TJob job = new() { Storage = data, Name = name };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(string baseFolderPath, Ulid libraryId)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { InputFolder = baseFolderPath, LibraryId = libraryId };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(Ulid libraryId, Guid id, Folder baseFolder, MediaFolderExtend mediaFolder)
        where TJob : AbstractReleaseJob, new()
    {
        TJob job = new() { LibraryId = libraryId, Id = id, BaseFolder = baseFolder, MediaFolder = mediaFolder };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(Guid id1, Guid? id2 = null, Guid? id3 = null)
        where TJob : AbstractFanArtDataJob, new()
    {
        TJob job = new() { Id1 = id1, Id2 = id2, Id3 = id3 };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(MusicBrainzReleaseGroup musicBrainzReleaseGroup)
        where TJob : MusicMetadataJob, new()
    {
        TJob job = new() { MusicBrainzReleaseGroup = musicBrainzReleaseGroup };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(MusicBrainzArtist musicBrainzArtist)
        where TJob : MusicMetadataJob, new()
    {
        TJob job = new() { MusicBrainzArtist = musicBrainzArtist };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(
        Guid id,
        Ulid folderId,
        FolderMetadata folderMetaData,
        MediaFile mediaFile,
        MusicBrainzTrack foundTrack,
        Ulid libraryId,
        string inputFolder,
        string inputFile
    )
        where TJob : MusicEncodeJob, new()
    {
        TJob job = new()
        {
            Id = id,
            FolderId = folderId,
            FoundTrack = foundTrack, FolderMetaData = folderMetaData,
            MediaFile = mediaFile, LibraryId = libraryId,
            InputFolder = inputFolder, InputFile = inputFile
        };
        Dispatcher.Dispatch(job);
    }

    public virtual void DispatchJob<TJob>(Track track)
        where TJob : AbstractLyricJob, new()
    {
        TJob job = new() { Track = track };
        Dispatcher.Dispatch(job);
    }
}
