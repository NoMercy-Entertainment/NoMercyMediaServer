using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Pipeline;

public record PreviewResult(
    bool Success,
    string OutputPath,
    TimeSpan Duration,
    EncodingMetrics Metrics,
    long OutputSizeBytes,
    EncodingError? Error
);
