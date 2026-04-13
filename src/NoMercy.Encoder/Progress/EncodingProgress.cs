namespace NoMercy.Encoder.Progress;

public record EncodingProgress(
    string CorrelationId,
    double PercentComplete,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining,
    double? CurrentFps,
    double? CurrentSpeed,
    string? CurrentStage,
    string? CurrentOperation,
    int? BitrateKbps = null
);
