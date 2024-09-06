using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem;

namespace NoMercy.MediaProcessing.Jobs;

public class JobDispatcher
{
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

    internal void DispatchJob<TJob, TChild>(int id, IEnumerable<TChild> jobItems)
        where TJob : AbstractPaletteJob<TChild>, new()
    {
        TJob job = new() { Id = id, Storage = jobItems.ToArray() };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
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
        TJob job = new() { FilePath = baseFolderPath, LibraryId = libraryId };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }

    public void DispatchJob<TJob>(Ulid libraryId, Guid id, Folder baseFolder, MediaFolder mediaFolder)
        where TJob : AbstractReleaseJob, new()
    {
        TJob job = new() { LibraryId = libraryId, Id = id, BaseFolder = baseFolder, MediaFolder = mediaFolder};
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }
    
    public void DispatchJob<TJob>(Guid id1, Guid? id2 = null, Guid? id3 = null)
        where TJob : AbstractFanArtDataJob, new()
    {
        TJob job = new() { Id1 = id1, Id2 = id2, Id3 = id3 };
        Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
    }
}