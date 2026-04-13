namespace NoMercy.Encoder.V3.Jobs;

using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Progress;

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
