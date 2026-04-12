namespace NoMercy.Encoder.V3.Pipeline;

using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Profiles;
using NoMercy.Encoder.V3.Progress;

public interface IEncoder
{
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );
}

public record EncodingRequest(
    string InputPath,
    string OutputDirectory,
    EncodingProfile Profile,
    EncodingOptions? Options = null
);

public record EncodingOptions(
    bool ResumeFromCheckpoint = false,
    int? MaxConcurrentEncodes = null,
    Priority Priority = Priority.Normal
);

public enum Priority
{
    Normal,
    High,
}

public record EncodingResult(
    bool Success,
    string OutputPath,
    TimeSpan Duration,
    EncodingError? Error,
    EncodingMetrics Metrics
);

public record EncodingMetrics(
    long OutputSizeBytes,
    double AverageSpeed,
    double AverageFps,
    string EncoderUsed,
    string? GpuUsed
);
