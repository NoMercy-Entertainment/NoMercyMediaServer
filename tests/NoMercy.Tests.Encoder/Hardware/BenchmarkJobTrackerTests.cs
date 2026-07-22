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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class BenchmarkJobTrackerTests
{
    private static IHostApplicationLifetime NeverStoppingLifetime()
    {
        Mock<IHostApplicationLifetime> lifetime = new();
        lifetime.Setup(expression: l => l.ApplicationStopping).Returns(value: CancellationToken.None);
        return lifetime.Object;
    }

    private static BenchmarkJobTracker MakeTracker(
        IHardwareBenchmark? benchmark = null,
        IHostApplicationLifetime? lifetime = null
    )
    {
        IHardwareBenchmark bench =
            benchmark
            ?? Mock.Of<IHardwareBenchmark>(predicate: b =>
                b.CalibrateAsync(It.IsAny<CancellationToken>())
                == Task.FromResult(new SpeedIndex(new()))
            );

        return new(
            benchmark: bench,
            logger: NullLogger<BenchmarkJobTracker>.Instance,
            lifetime: lifetime ?? NeverStoppingLifetime()
        );
    }

    // ── Start ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Start_returns_status_with_job_id_and_running()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus status = tracker.Start(codecs: [], resolutions: []);

        status.JobId.Should().NotBeNullOrWhiteSpace();
        status.Status.Should().Be(expected: "running");
        status.CompletedAt.Should().BeNull();
        status.Error.Should().BeNull();
    }

    [Fact]
    public void Start_records_requested_codecs_and_resolutions()
    {
        BenchmarkJobTracker tracker = MakeTracker();
        IReadOnlyList<VideoCodecType> codecs = [VideoCodecType.H264, VideoCodecType.Av1];
        IReadOnlyList<int> resolutions = [1920, 1280];

        BenchmarkJobStatus status = tracker.Start(codecs: codecs, resolutions: resolutions);

        status.RequestedCodecs.Should().BeEquivalentTo(expectation: ["H264", "Av1"]);
        status.RequestedResolutions.Should().BeEquivalentTo(expectation: [1920, 1280]);
    }

    // ── Get ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_returns_null_for_unknown_job_id()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus? result = tracker.Get(jobId: "does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public void Get_returns_status_for_known_job()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus started = tracker.Start(codecs: [], resolutions: []);
        BenchmarkJobStatus? fetched = tracker.Get(jobId: started.JobId);

        fetched.Should().NotBeNull();
        fetched!.JobId.Should().Be(expected: started.JobId);
    }

    // ── List ───────────────────────────────────────────────────────────────────

    [Fact]
    public void List_returns_all_started_jobs()
    {
        BenchmarkJobTracker tracker = MakeTracker();

        BenchmarkJobStatus a = tracker.Start(codecs: [], resolutions: []);
        BenchmarkJobStatus b = tracker.Start(codecs: [VideoCodecType.H265], resolutions: []);

        IReadOnlyList<BenchmarkJobStatus> all = tracker.List();

        all.Should().HaveCount(expected: 2);
        all.Select(selector: j => j.JobId).Should().Contain(expected: [a.JobId, b.JobId]);
    }

    // ── Async lifecycle ────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_kicks_off_benchmark_async()
    {
        Mock<IHardwareBenchmark> mock = new();
        TaskCompletionSource<SpeedIndex> tcs = new();
        mock.Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>())).Returns(value: tcs.Task);

        BenchmarkJobTracker tracker = MakeTracker(benchmark: mock.Object);

        BenchmarkJobStatus job = tracker.Start(codecs: [], resolutions: []);

        // Resolve the task so the background work can complete.
        tcs.SetResult(result: new(Measurements: new()));

        // Allow the background Task.Run to finish.
        await Task.Delay(millisecondsDelay: 200);

        mock.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Once);

        BenchmarkJobStatus? updated = tracker.Get(jobId: job.JobId);
        updated!.Status.Should().Be(expected: "completed");
    }

    [Fact]
    public async Task Failed_benchmark_marks_status_failed_with_error_message()
    {
        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new InvalidOperationException(message: "ffmpeg not found"));

        BenchmarkJobTracker tracker = MakeTracker(benchmark: mock.Object);

        BenchmarkJobStatus job = tracker.Start(codecs: [], resolutions: []);

        await Task.Delay(millisecondsDelay: 300);

        BenchmarkJobStatus? updated = tracker.Get(jobId: job.JobId);
        updated!.Status.Should().Be(expected: "failed");
        updated.Error.Should().Contain(expected: "ffmpeg not found");
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Status_progresses_running_then_completed_with_measurement_count()
    {
        Dictionary<SpeedKey, SpeedMeasurement> measurements = new()
        {
            [key: new(Codec: VideoCodecType.H264, Encoder: "libx264", Width: 1920, DeviceName: null)] = new(
                Fps: 120,
                SpeedMultiplier: 4.0,
                MeasuredAt: DateTime.UtcNow
            ),
            [key: new(Codec: VideoCodecType.H265, Encoder: "libx265", Width: 1920, DeviceName: null)] = new(
                Fps: 60,
                SpeedMultiplier: 2.0,
                MeasuredAt: DateTime.UtcNow
            ),
        };

        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new SpeedIndex(Measurements: measurements));

        BenchmarkJobTracker tracker = MakeTracker(benchmark: mock.Object);

        BenchmarkJobStatus initial = tracker.Start(codecs: [], resolutions: []);
        initial.Status.Should().Be(expected: "running");
        initial.MeasurementCount.Should().Be(expected: 0);

        await Task.Delay(millisecondsDelay: 300);

        BenchmarkJobStatus? completed = tracker.Get(jobId: initial.JobId);
        completed!.Status.Should().Be(expected: "completed");
        completed.MeasurementCount.Should().Be(expected: 2);
        completed.CompletedAt.Should().NotBeNull();
    }

    // ── Single-flight calibration ────────────────────────────────────────────
    //
    // Start() can be triggered from several independent places close together
    // (driver-change detection, the on-demand HTTP endpoint, a retried click).
    // Without the SemaphoreSlim gate each spawns its own concurrent ffmpeg
    // probe and they race writing the same SpeedIndex cache file.

    [Fact]
    public async Task Start_ConcurrentTriggers_NeverRunCalibrateAsyncConcurrently()
    {
        int concurrentCount = 0;
        int maxObservedConcurrency = 0;
        object gate = new();

        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(valueFunction: async () =>
            {
                lock (gate)
                {
                    concurrentCount++;
                    maxObservedConcurrency = Math.Max(val1: maxObservedConcurrency, val2: concurrentCount);
                }

                await Task.Delay(millisecondsDelay: 75);

                lock (gate)
                {
                    concurrentCount--;
                }

                return new(Measurements: new());
            });

        BenchmarkJobTracker tracker = MakeTracker(benchmark: mock.Object);

        BenchmarkJobStatus jobA = tracker.Start(codecs: [], resolutions: []);
        BenchmarkJobStatus jobB = tracker.Start(codecs: [], resolutions: []);
        BenchmarkJobStatus jobC = tracker.Start(codecs: [], resolutions: []);

        await Task.Delay(millisecondsDelay: 700);

        maxObservedConcurrency.Should().Be(expected: 1);
        tracker.Get(jobId: jobA.JobId)!.Status.Should().Be(expected: "completed");
        tracker.Get(jobId: jobB.JobId)!.Status.Should().Be(expected: "completed");
        tracker.Get(jobId: jobC.JobId)!.Status.Should().Be(expected: "completed");
    }

    // ── Host shutdown token ──────────────────────────────────────────────────
    //
    // RunAsync used to hardcode CancellationToken.None — a benchmark could
    // outlive the host it was calibrating for.

    [Fact]
    public async Task RunAsync_PassesLifetimeApplicationStoppingToken_ToCalibrateAsync()
    {
        using CancellationTokenSource shutdownCts = new();
        Mock<IHostApplicationLifetime> lifetime = new();
        lifetime.Setup(expression: l => l.ApplicationStopping).Returns(value: shutdownCts.Token);

        CancellationToken? observedToken = null;
        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(
                valueFunction: (CancellationToken ct) =>
                {
                    observedToken = ct;
                    return Task.FromResult(result: new SpeedIndex(Measurements: new()));
                }
            );

        BenchmarkJobTracker tracker = MakeTracker(benchmark: mock.Object, lifetime: lifetime.Object);

        tracker.Start(codecs: [], resolutions: []);
        await Task.Delay(millisecondsDelay: 200);

        observedToken.Should().Be(expected: shutdownCts.Token);
    }

    [Fact]
    public async Task RunAsync_ShutdownWhileQueuedBehindGate_CancelsWithoutCallingCalibrateAsync()
    {
        TaskCompletionSource firstJobStarted = new(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource<SpeedIndex> firstJobRelease = new(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
        );
        int calibrateCallCount = 0;

        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(valueFunction: async () =>
            {
                Interlocked.Increment(location: ref calibrateCallCount);
                firstJobStarted.TrySetResult();
                return await firstJobRelease.Task;
            });

        using CancellationTokenSource shutdownCts = new();
        Mock<IHostApplicationLifetime> lifetime = new();
        lifetime.Setup(expression: l => l.ApplicationStopping).Returns(value: shutdownCts.Token);

        BenchmarkJobTracker tracker = MakeTracker(benchmark: mock.Object, lifetime: lifetime.Object);

        BenchmarkJobStatus first = tracker.Start(codecs: [], resolutions: []);
        await firstJobStarted.Task; // first job now holds the gate, blocked inside CalibrateAsync

        BenchmarkJobStatus second = tracker.Start(codecs: [], resolutions: []);
        await Task.Delay(millisecondsDelay: 150); // let the second job queue behind _calibrationGate

        await shutdownCts.CancelAsync();
        await Task.Delay(millisecondsDelay: 150);

        tracker.Get(jobId: second.JobId)!.Status.Should().Be(expected: "cancelled");
        tracker.Get(jobId: first.JobId)!.Status.Should().Be(expected: "running");

        // Release the first job so the test doesn't leak a running task.
        firstJobRelease.TrySetResult(result: new(Measurements: new()));
        await Task.Delay(millisecondsDelay: 150);

        calibrateCallCount.Should().Be(expected: 1); // second job's CalibrateAsync was never invoked
    }
}
