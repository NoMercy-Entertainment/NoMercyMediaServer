// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.Hardware;

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
    ILogger<BenchmarkJobTracker> logger,
    IHostApplicationLifetime lifetime
) : IBenchmarkJobTracker
{
    // Bounds the in-memory job history. Start() is triggered repeatedly over a
    // long uptime (boot driver-change detection, the HTTP endpoint, user retries,
    // 30-day recalibration); without a cap this dictionary grows forever. The
    // durable result lives in ISpeedIndexStore, so this map is observability only.
    private const int MaxRetainedJobs = 100;

    private readonly ConcurrentDictionary<string, BenchmarkJobStatus> _jobs = new();

    // Start() can be triggered from several independent places (driver-change
    // detection at boot, the on-demand HTTP endpoint, a user retrying it) close
    // together. Without this gate each spawns its own concurrent ffmpeg
    // hardware probe and they race writing the same SpeedIndex cache file. One
    // calibration runs at a time; the rest queue behind the semaphore in call
    // order.
    private readonly SemaphoreSlim _calibrationGate = new(1, 1);

    public BenchmarkJobStatus Start(
        IReadOnlyList<Codecs.VideoCodecType> codecs,
        IReadOnlyList<int> resolutions
    )
    {
        string jobId = Ulid.NewUlid().ToString();
        List<string> codecNames = codecs.Select(c => c.ToString()).ToList();

        BenchmarkJobStatus initial = new(
            jobId,
            "running",
            DateTime.UtcNow,
            null,
            0,
            codecNames,
            resolutions.ToList(),
            null
        );

        _jobs[jobId] = initial;
        TrimHistory();

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

    private void TrimHistory()
    {
        foreach (string jobId in EvictionCandidates(_jobs.Values, MaxRetainedJobs))
            _jobs.TryRemove(jobId, out _);
    }

    /// <summary>
    /// The oldest completed/failed/cancelled job ids to drop when the retained
    /// count exceeds <paramref name="maxRetained"/>. Running jobs are never
    /// evicted (they still need their status observed).
    /// </summary>
    public static IEnumerable<string> EvictionCandidates(
        ICollection<BenchmarkJobStatus> jobs,
        int maxRetained
    )
    {
        if (jobs.Count <= maxRetained)
            return [];

        return jobs.Where(job => job.CompletedAt is not null)
            .OrderBy(job => job.CompletedAt)
            .Take(jobs.Count - maxRetained)
            .Select(job => job.JobId)
            .ToList();
    }

    private async Task RunAsync(
        string jobId,
        IReadOnlyList<string> codecNames,
        IReadOnlyList<int> resolutions
    )
    {
        CancellationToken shutdownToken = lifetime.ApplicationStopping;

        try
        {
            await _calibrationGate.WaitAsync(shutdownToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _jobs[jobId] = _jobs[jobId] with
            {
                Status = "cancelled",
                CompletedAt = DateTime.UtcNow,
            };

            logger.LogInformation(
                "Benchmark job {JobId} cancelled before it could start (host shutting down)",
                jobId
            );
            return;
        }

        try
        {
            logger.LogInformation(
                "Benchmark job {JobId} started (requested codecs: [{Codecs}])", [jobId, string.Join(", ", codecNames.Count > 0 ? codecNames : ["all"])]
            );

            SpeedIndex result = await benchmark.CalibrateAsync(shutdownToken);

            _jobs[jobId] = _jobs[jobId] with
            {
                Status = "completed",
                CompletedAt = DateTime.UtcNow,
                MeasurementCount = result.Measurements.Count,
            };

            logger.LogInformation(
                "Benchmark job {JobId} completed — {Count} measurements", [jobId, result.Measurements.Count]
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
        finally
        {
            _calibrationGate.Release();
        }
    }
}
