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
    ILogger<BenchmarkJobTracker> logger
) : IBenchmarkJobTracker
{
    private readonly ConcurrentDictionary<string, BenchmarkJobStatus> _jobs = new();

    public BenchmarkJobStatus Start(
        IReadOnlyList<Codecs.VideoCodecType> codecs,
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
