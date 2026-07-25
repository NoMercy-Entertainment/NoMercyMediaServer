using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public abstract class AbstractJob : IShouldQueue
{
    public abstract string QueueName { get; }
    public abstract int Priority { get; }

    public abstract Task Handle();

    public void Dispose() { }
}