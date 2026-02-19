using NoMercy.Database.Models.Music;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public abstract class AbstractLyricJob : IShouldQueue
{
    public int Id { get; set; }
    public Track Track { get; set; } = null!;

    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void Dispose()
    {
    }
}