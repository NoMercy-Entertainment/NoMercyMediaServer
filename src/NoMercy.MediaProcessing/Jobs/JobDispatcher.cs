using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Queue;

namespace NoMercy.MediaProcessing.Jobs;

public class JobDispatcher
{
    public void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string id, string inputFile)
        where TJob : AbstractEncoderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, FolderId = folderId, Id = id, InputFile = inputFile };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, Guid releaseId, string filePath)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, ReleaseId = releaseId, InputFolder = filePath };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string filePath)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { LibraryId = libraryId, FolderId = folderId, InputFolder = filePath };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(int id, Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = libraryId };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(Ulid libraryId)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { LibraryId = libraryId };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(int id, Library library, int? priority = null)
        where TJob : AbstractMediaJob, new()
    {
        TJob job = new() { Id = id, LibraryId = library.Id };
        QueueRunner.Dispatcher.Dispatch(job, job.QueueName, priority ?? job.Priority);
    }

    internal void DispatchJob<TJob, TChild>(TChild data)
        where TJob : AbstractMediaExraDataJob<TChild>, new()
    {
        TJob job = new() { Storage = data };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    internal void DispatchJob<TJob, TChild>(IEnumerable<TChild> data, string name)
        where TJob : AbstractShowExtraDataJob<TChild, string>, new()
    {
        TJob job = new() { Storage = data, Name = name };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(string baseFolderPath, Ulid libraryId)
        where TJob : AbstractMusicFolderJob, new()
    {
        TJob job = new() { InputFolder = baseFolderPath, LibraryId = libraryId };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(Ulid libraryId, Guid id, Folder baseFolder, MediaFolderExtend mediaFolder)
        where TJob : AbstractReleaseJob, new()
    {
        TJob job = new() { LibraryId = libraryId, Id = id, BaseFolder = baseFolder, MediaFolder = mediaFolder };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(Guid id1, Guid? id2 = null, Guid? id3 = null)
        where TJob : AbstractFanArtDataJob, new()
    {
        TJob job = new() { Id1 = id1, Id2 = id2, Id3 = id3 };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(MusicBrainzReleaseGroup musicBrainzReleaseGroup)
        where TJob : MusicDescriptionJob, new()
    {
        TJob job = new() { MusicBrainzReleaseGroup = musicBrainzReleaseGroup };
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(MusicBrainzArtist musicBrainzArtist)
        where TJob : MusicDescriptionJob, new()
    {
        TJob job = new() { MusicBrainzArtist = musicBrainzArtist };
        QueueRunner.Dispatcher.Dispatch(job);
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
        QueueRunner.Dispatcher.Dispatch(job);
    }

    public void DispatchJob<TJob>(Track track)
        where TJob : AbstractLyricJob, new()
    {
        TJob job = new() { Track = track };
        QueueRunner.Dispatcher.Dispatch(job);
    }
}
