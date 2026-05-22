namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Snapshot of a single benchmark job's lifecycle.
/// Codecs and Resolutions record what the caller requested; the underlying
/// <see cref="IHardwareBenchmark.CalibrateAsync"/> always runs all codecs
/// (codec/resolution filtering is a forward-looking feature not yet wired
/// into the benchmark engine).
/// </summary>
public sealed record BenchmarkJobStatus(
    string JobId,
    string Status, // "queued" | "running" | "completed" | "failed" | "cancelled"
    DateTime StartedAt,
    DateTime? CompletedAt,
    int MeasurementCount, // 0 until completed
    IReadOnlyList<string> RequestedCodecs,
    IReadOnlyList<int> RequestedResolutions,
    string? Error // non-null when failed
);
