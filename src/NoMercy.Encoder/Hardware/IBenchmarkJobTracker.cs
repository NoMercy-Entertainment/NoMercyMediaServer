using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;

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

public interface IBenchmarkJobTracker
{
    /// <summary>
    /// Starts a new benchmark job and returns its initial status immediately.
    /// The benchmark runs asynchronously in the background.
    /// </summary>
    BenchmarkJobStatus Start(IReadOnlyList<VideoCodecType> codecs, IReadOnlyList<int> resolutions);

    /// <summary>Returns the current status of a job, or null if the id is unknown.</summary>
    BenchmarkJobStatus? Get(string jobId);

    /// <summary>Returns a snapshot of all known jobs.</summary>
    IReadOnlyList<BenchmarkJobStatus> List();
}

/// <summary>
/// In-memory singleton tracker for benchmark jobs.
/// State is process-local — a server restart loses history, which is acceptable
/// because the durable result (SpeedIndex) is persisted separately via
/// <c>ISpeedIndexStore</c>.
///
/// NOTE: <see cref="IHardwareBenchmark.CalibrateAsync"/> does not currently
/// accept codec/resolution filters. The requested values from the HTTP body
/// are stored in the job status for observability, but the benchmark engine
/// always calibrates all available codecs.
/// </summary>
public sealed class BenchmarkJobTracker(
    IHardwareBenchmark benchmark,
    ILogger<BenchmarkJobTracker> logger
) : IBenchmarkJobTracker
{
    private readonly ConcurrentDictionary<string, BenchmarkJobStatus> _jobs = new();

    public BenchmarkJobStatus Start(
        IReadOnlyList<VideoCodecType> codecs,
        IReadOnlyList<int> resolutions
    )
    {
        string jobId = Ulid.NewUlid().ToString();
        List<string> codecNames = codecs.Select(c => c.ToString()).ToList();

        BenchmarkJobStatus initial = new(
            JobId: jobId,
            Status: "running",
            StartedAt: DateTime.UtcNow,
            CompletedAt: null,
            MeasurementCount: 0,
            RequestedCodecs: codecNames,
            RequestedResolutions: resolutions.ToList(),
            Error: null
        );

        _jobs[jobId] = initial;

        // Fire-and-forget — do NOT await; caller gets the job id immediately.
        _ = Task.Run(async () => await RunAsync(jobId, codecNames, resolutions));

        return initial;
    }

    public BenchmarkJobStatus? Get(string jobId)
    {
        _jobs.TryGetValue(jobId, out BenchmarkJobStatus? status);
        return status;
    }

    public IReadOnlyList<BenchmarkJobStatus> List() => _jobs.Values.ToList();

    private async Task RunAsync(
        string jobId,
        IReadOnlyList<string> codecNames,
        IReadOnlyList<int> resolutions
    )
    {
        try
        {
            logger.LogInformation(
                "Benchmark job {JobId} started (requested codecs: [{Codecs}])",
                jobId,
                string.Join(", ", codecNames.Count > 0 ? codecNames : ["all"])
            );

            SpeedIndex result = await benchmark.CalibrateAsync(CancellationToken.None);

            _jobs[jobId] = _jobs[jobId] with
            {
                Status = "completed",
                CompletedAt = DateTime.UtcNow,
                MeasurementCount = result.Measurements.Count,
            };

            logger.LogInformation(
                "Benchmark job {JobId} completed — {Count} measurements",
                jobId,
                result.Measurements.Count
            );
        }
        catch (OperationCanceledException)
        {
            _jobs[jobId] = _jobs[jobId] with
            {
                Status = "cancelled",
                CompletedAt = DateTime.UtcNow,
            };

            logger.LogInformation("Benchmark job {JobId} was cancelled", jobId);
        }
        catch (Exception ex)
        {
            _jobs[jobId] = _jobs[jobId] with
            {
                Status = "failed",
                CompletedAt = DateTime.UtcNow,
                Error = ex.Message,
            };

            logger.LogError(ex, "Benchmark job {JobId} failed", jobId);
        }
    }
}
