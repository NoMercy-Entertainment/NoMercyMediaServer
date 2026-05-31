namespace NoMercy.Encoder.Hardware;

public interface IHardwareBenchmark
{
    SpeedIndex GetCachedIndex();
    Task<SpeedIndex> CalibrateAsync(CancellationToken ct);
    bool NeedsRecalibration();

    /// <summary>
    /// True while a calibration run is in progress. The dashboard polls
    /// this so it can tighten its refetch interval (5 s while running, 30 s
    /// idle) and surface a "benchmarking..." indicator to the user instead
    /// of letting the table look frozen for the ~10 min run.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Snapshot of the current calibration progress: 0–100 percent, plus a
    /// human-readable stage string ("h264_nvenc 1920x1080") and counts.
    /// Updated as each per-probe Report fires. Null when no run has
    /// happened since service start.
    /// </summary>
    BenchmarkProgress? CurrentProgress { get; }
}

/// <summary>Live-progress snapshot for the dashboard "benchmark running" UI.</summary>
public sealed record BenchmarkProgress(
    double PercentComplete,
    int CompletedProbes,
    int TotalProbes,
    string Stage,
    DateTime UpdatedAt
);
