namespace NoMercy.Encoder.V3.Progress;

public record EncodingProgress(
    string CorrelationId,
    double PercentComplete,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining,
    double? CurrentFps,
    double? CurrentSpeed,
    string? CurrentStage,
    string? CurrentOperation
);
