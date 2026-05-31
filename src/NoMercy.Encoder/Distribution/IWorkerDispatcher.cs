namespace NoMercy.Encoder.Distribution;

public interface IWorkerDispatcher
{
    /// <summary>
    /// Runs <paramref name="tasks"/> across available workers. The dispatcher
    /// decides whether to run locally, split across remote workers, or a mix.
    /// Returns one <see cref="DispatchResult"/> per input task in the same
    /// order as <paramref name="tasks"/>. A partial failure reports the
    /// specific task's success=false; callers decide whether to retry or
    /// abort the surrounding encode.
    /// </summary>
    Task<DispatchResult[]> DispatchAsync(EncodeTask[] tasks, CancellationToken ct);

    /// <summary>Number of workers available to this dispatcher right now.</summary>
    int AvailableWorkerCount { get; }
}
