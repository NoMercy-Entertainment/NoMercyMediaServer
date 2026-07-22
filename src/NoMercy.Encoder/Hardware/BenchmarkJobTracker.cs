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
    private readonly SemaphoreSlim _calibrationGate = new(initialCount: 1, maxCount: 1);

    public BenchmarkJobStatus Start(
        IReadOnlyList<Codecs.VideoCodecType> codecs,
        IReadOnlyList<int> resolutions
    )
    {
        string jobId = Ulid.NewUlid().ToString();
        List<string> codecNames = codecs.Select(selector: c => c.ToString()).ToList();

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

        _jobs[key: jobId] = initial;
        TrimHistory();

        // Fire-and-forget — do NOT await; caller gets the job id immediately.
        _ = Task.Run(function: async () => await RunAsync(jobId: jobId, codecNames: codecNames, resolutions: resolutions));

        return initial;
    }

    public BenchmarkJobStatus? Get(string jobId)
    {
        _jobs.TryGetValue(key: jobId, value: out BenchmarkJobStatus? status);
        return status;
    }

    public IReadOnlyList<BenchmarkJobStatus> List() => _jobs.Values.ToList();

    private void TrimHistory()
    {
        foreach (string jobId in EvictionCandidates(jobs: _jobs.Values, maxRetained: MaxRetainedJobs))
            _jobs.TryRemove(key: jobId, value: out _);
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

        return jobs.Where(predicate: job => job.CompletedAt is not null)
            .OrderBy(keySelector: job => job.CompletedAt)
            .Take(count: jobs.Count - maxRetained)
            .Select(selector: job => job.JobId)
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
            await _calibrationGate.WaitAsync(cancellationToken: shutdownToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            _jobs[key: jobId] = _jobs[key: jobId] with
            {
                Status = "cancelled",
                CompletedAt = DateTime.UtcNow,
            };

            logger.LogInformation(
                message: "Benchmark job {JobId} cancelled before it could start (host shutting down)",
                args: jobId
            );
            return;
        }

        try
        {
            logger.LogInformation(
                message: "Benchmark job {JobId} started (requested codecs: [{Codecs}])", args: [jobId, string.Join(separator: ", ", values: codecNames.Count > 0 ? codecNames : ["all"])]
            );

            SpeedIndex result = await benchmark.CalibrateAsync(ct: shutdownToken);

            _jobs[key: jobId] = _jobs[key: jobId] with
            {
                Status = "completed",
                CompletedAt = DateTime.UtcNow,
                MeasurementCount = result.Measurements.Count,
            };

            logger.LogInformation(
                message: "Benchmark job {JobId} completed — {Count} measurements", args: [jobId, result.Measurements.Count]
            );
        }
        catch (OperationCanceledException)
        {
            _jobs[key: jobId] = _jobs[key: jobId] with
            {
                Status = "cancelled",
                CompletedAt = DateTime.UtcNow,
            };

            logger.LogInformation(message: "Benchmark job {JobId} was cancelled", args: jobId);
        }
        catch (Exception ex)
        {
            _jobs[key: jobId] = _jobs[key: jobId] with
            {
                Status = "failed",
                CompletedAt = DateTime.UtcNow,
                Error = ex.Message,
            };

            logger.LogError(exception: ex, message: "Benchmark job {JobId} failed", args: jobId);
        }
        finally
        {
            _calibrationGate.Release();
        }
    }
}
