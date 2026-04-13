namespace NoMercy.Encoder.Jobs;

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

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

    IHardwareCapabilities GetCapabilities();

    ResourceBudgetSnapshot GetAvailableBudget();
}
