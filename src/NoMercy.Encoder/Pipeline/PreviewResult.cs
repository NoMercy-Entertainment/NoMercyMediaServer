namespace NoMercy.Encoder.Pipeline;

using NoMercy.Encoder.Errors;

public record PreviewResult(
    bool Success,
    string OutputPath,
    TimeSpan Duration,
    EncodingMetrics Metrics,
    long OutputSizeBytes,
    EncodingError? Error
);
