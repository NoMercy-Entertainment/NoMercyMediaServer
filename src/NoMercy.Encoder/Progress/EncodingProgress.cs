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
    int? BitrateKbps = null,
    string? Bitrate = null,
    int ProcessId = 0,
    double CurrentTimeSeconds = 0,
    double DurationSeconds = 0
);
