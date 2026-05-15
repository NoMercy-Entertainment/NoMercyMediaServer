namespace NoMercyQueue.Core.Interfaces;

public interface IShouldQueue
{
    string QueueName { get; }
    int Priority { get; }
    Task Handle();
}

/// <summary>
/// Optional companion to <see cref="IShouldQueue"/>. When a job implements
/// this interface the queue worker calls <see cref="ReceiveJobId"/> with the
/// persisted queue-job ID immediately before invoking <see cref="IShouldQueue.Handle"/>.
///
/// Used by coordinator jobs (e.g. <c>VideoEncodeJob</c>) that need their own
/// queue-job ID to stamp child jobs with the correct <c>ParentJobId</c>.
/// </summary>
public interface IJobIdReceiver
{
    void ReceiveJobId(int jobId);
}
