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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class BenchmarkJobTrackerTests
{
    private static BenchmarkJobTracker MakeTracker(IHardwareBenchmark? benchmark = null)
    {
        IHardwareBenchmark bench =
            benchmark
            ?? Mock.Of<IHardwareBenchmark>(b =>
                b.CalibrateAsync(It.IsAny<CancellationToken>())
                == Task.FromResult(new SpeedIndex(new()))
            );

        return new BenchmarkJobTracker(bench, NullLogger<BenchmarkJobTracker>.Instance);
    }

    // ── Start ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Start_returns_status_with_job_id_and_running()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus status = tracker.Start([], []);

        status.JobId.Should().NotBeNullOrWhiteSpace();
        status.Status.Should().Be("running");
        status.CompletedAt.Should().BeNull();
        status.Error.Should().BeNull();
    }

    [Fact]
    public void Start_records_requested_codecs_and_resolutions()
    {
        BenchmarkJobTracker tracker = MakeTracker();
        IReadOnlyList<VideoCodecType> codecs = [VideoCodecType.H264, VideoCodecType.Av1];
        IReadOnlyList<int> resolutions = [1920, 1280];

        BenchmarkJobStatus status = tracker.Start(codecs, resolutions);

        status.RequestedCodecs.Should().BeEquivalentTo("H264", "Av1");
        status.RequestedResolutions.Should().BeEquivalentTo([1920, 1280]);
    }

    // ── Get ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_returns_null_for_unknown_job_id()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus? result = tracker.Get("does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public void Get_returns_status_for_known_job()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus started = tracker.Start([], []);
        BenchmarkJobStatus? fetched = tracker.Get(started.JobId);

        fetched.Should().NotBeNull();
        fetched!.JobId.Should().Be(started.JobId);
    }

    // ── List ───────────────────────────────────────────────────────────────────

    [Fact]
    public void List_returns_all_started_jobs()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus a = tracker.Start([], []);
        BenchmarkJobStatus b = tracker.Start([VideoCodecType.H265], []);

        IReadOnlyList<BenchmarkJobStatus> all = tracker.List();

        all.Should().HaveCount(2);
        all.Select(j => j.JobId).Should().Contain([a.JobId, b.JobId]);
    }

    // ── Async lifecycle ────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_kicks_off_benchmark_async()
    {
        Mock<IHardwareBenchmark> mock = new();
        TaskCompletionSource<SpeedIndex> tcs = new();
        mock.Setup(b => b.CalibrateAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        BenchmarkJobTracker tracker = MakeTracker(mock.Object);

        BenchmarkJobStatus job = tracker.Start([], []);

        // Resolve the task so the background work can complete.
        tcs.SetResult(new SpeedIndex(new()));

        // Allow the background Task.Run to finish.
        await Task.Delay(200);

        mock.Verify(b => b.CalibrateAsync(It.IsAny<CancellationToken>()), Times.Once);

        BenchmarkJobStatus? updated = tracker.Get(job.JobId);
        updated!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task Failed_benchmark_marks_status_failed_with_error_message()
    {
        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ffmpeg not found"));

        BenchmarkJobTracker tracker = MakeTracker(mock.Object);

        BenchmarkJobStatus job = tracker.Start([], []);

        await Task.Delay(300);

        BenchmarkJobStatus? updated = tracker.Get(job.JobId);
        updated!.Status.Should().Be("failed");
        updated.Error.Should().Contain("ffmpeg not found");
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Status_progresses_running_then_completed_with_measurement_count()
    {
        Dictionary<SpeedKey, SpeedMeasurement> measurements = new()
        {
            [new(VideoCodecType.H264, "libx264", 1920, null)] = new(
                Fps: 120,
                SpeedMultiplier: 4.0,
                MeasuredAt: DateTime.UtcNow
            ),
            [new(VideoCodecType.H265, "libx265", 1920, null)] = new(
                Fps: 60,
                SpeedMultiplier: 2.0,
                MeasuredAt: DateTime.UtcNow
            ),
        };

        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeedIndex(measurements));

        BenchmarkJobTracker tracker = MakeTracker(mock.Object);

        BenchmarkJobStatus initial = tracker.Start([], []);
        initial.Status.Should().Be("running");
        initial.MeasurementCount.Should().Be(0);

        await Task.Delay(300);

        BenchmarkJobStatus? completed = tracker.Get(initial.JobId);
        completed!.Status.Should().Be("completed");
        completed.MeasurementCount.Should().Be(2);
        completed.CompletedAt.Should().NotBeNull();
    }
}
