using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Jobs;

public record ResourceBudgetSnapshot(
    int AvailableGpuSlots,
    int AvailableCpuThreads,
    double GpuUtilization
);

public record RemoteEncodingResult(
    bool Success,
    string? WorkerId,
    string OutputPath,
    TimeSpan Duration,
    EncodingError? Error,
    EncodingMetrics? Metrics
);

public interface IRemoteWorker
{
    string WorkerId { get; }

    Task<RemoteEncodingResult> ExecuteJobAsync(
        EncodingJob job,
        IProgress<EncodingProgress> progress,
        CancellationToken ct
    );

    /// <summary>
    /// Runs a single <see cref="EncodeTask"/> (one variant or one time
    /// chunk) on the remote worker and returns the dispatch result.
    /// This is the unit the <see cref="RemoteWorkerDispatcher"/>
    /// splits work into — a job that needs 4 variants becomes 4 tasks,
    /// each potentially landing on a different worker.
    ///
    /// Implementations must be safe against out-of-band cancellation:
    /// when <paramref name="ct"/> fires the worker should abort the
    /// encode, clean up any partial output, and return a DispatchResult
    /// with <see cref="DispatchResult.Success"/> = false.
    /// </summary>
    Task<DispatchResult> ExecuteTaskAsync(EncodeTask task, CancellationToken ct);

    IHardwareCapabilities GetCapabilities();

    ResourceBudgetSnapshot GetAvailableBudget();
}
