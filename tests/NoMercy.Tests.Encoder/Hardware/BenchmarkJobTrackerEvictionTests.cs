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

using NoMercy.Encoder.Hardware;
using Xunit;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// BenchmarkJobTracker._jobs was never pruned, so repeated Start() calls over a
/// long uptime grew it without bound. EvictionCandidates decides what to drop.
/// </summary>
public class BenchmarkJobTrackerEvictionTests
{
    private static BenchmarkJobStatus Job(string id, DateTime? completedAt) =>
        new(
            JobId: id,
            Status: completedAt is null ? "running" : "completed",
            StartedAt: DateTime.UtcNow,
            CompletedAt: completedAt,
            MeasurementCount: 0,
            RequestedCodecs: [],
            RequestedResolutions: [],
            Error: null
        );

    [Fact]
    public void EvictionCandidates_AtOrUnderCap_EvictsNothing()
    {
        List<BenchmarkJobStatus> jobs = [Job(id: "a", completedAt: DateTime.UtcNow), Job(id: "b", completedAt: DateTime.UtcNow)];

        Assert.Empty(collection: BenchmarkJobTracker.EvictionCandidates(jobs: jobs, maxRetained: 100));
    }

    [Fact]
    public void EvictionCandidates_OverCap_DropsOldestCompletedFirst()
    {
        DateTime baseTime = new(year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);
        List<BenchmarkJobStatus> jobs = [];
        for (int i = 0; i < 105; i++)
            jobs.Add(item: Job(id: $"job{i:D3}", completedAt: baseTime.AddMinutes(value: i)));

        List<string> evicted = BenchmarkJobTracker.EvictionCandidates(jobs: jobs, maxRetained: 100).ToList();

        Assert.Equal(expected: 5, actual: evicted.Count);
        Assert.Equal(expected: ["job000", "job001", "job002", "job003", "job004"], actual: evicted);
    }

    [Fact]
    public void EvictionCandidates_NeverEvictsRunningJobs()
    {
        DateTime baseTime = new(year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);
        List<BenchmarkJobStatus> jobs = [];
        for (int i = 0; i < 96; i++)
            jobs.Add(item: Job(id: $"done{i:D3}", completedAt: baseTime.AddMinutes(value: i)));
        for (int i = 0; i < 10; i++)
            jobs.Add(item: Job(id: $"run{i:D2}", completedAt: null));

        List<string> evicted = BenchmarkJobTracker.EvictionCandidates(jobs: jobs, maxRetained: 100).ToList();

        Assert.Equal(expected: 6, actual: evicted.Count);
        Assert.All(collection: evicted, action: id => Assert.StartsWith(expectedStartString: "done", actualString: id));
    }
}
