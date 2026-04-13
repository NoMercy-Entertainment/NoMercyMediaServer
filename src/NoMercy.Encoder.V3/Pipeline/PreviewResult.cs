namespace NoMercy.Encoder.V3.Pipeline;

using NoMercy.Encoder.V3.Errors;

public record PreviewResult(
    bool Success,
    string OutputPath,
    TimeSpan Duration,
    EncodingMetrics Metrics,
    long OutputSizeBytes,
    EncodingError? Error
);
