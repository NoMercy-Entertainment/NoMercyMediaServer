using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Execution;

public record ExecutionResult(
    bool Success,
    int ExitCode,
    string StdErr,
    TimeSpan Duration,
    EncodingError? Error,
    ExecutionMetrics? Metrics = null
);

public record ExecutionMetrics(
    double AverageSpeed,
    double AverageFps,
    double PeakSpeed,
    double PeakFps,
    long TotalSizeBytes
);
