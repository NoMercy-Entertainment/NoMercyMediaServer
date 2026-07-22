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
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Startup;
using NoMercy.NmSystem.Lifecycle;

namespace NoMercy.Tests.Encoder.Startup;

public class HardwareBenchmarkBackgroundServiceTests
{
    [Fact]
    public async Task AutoCalibrateOff_ExitsWithoutCalibrating()
    {
        Mock<IHardwareBenchmark> benchmark = new();
        HardwareBenchmarkBackgroundService sut = NewService(
            benchmark: benchmark.Object,
            autoCalibrate: false,
            startedNow: true
        );

        await sut.StartAsync(cancellationToken: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50);
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task FreshCache_ExitsWithoutCalibrating()
    {
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(expression: b => b.NeedsRecalibration()).Returns(value: false);

        HardwareBenchmarkBackgroundService sut = NewService(
            benchmark: benchmark.Object,
            autoCalibrate: true,
            startedNow: true
        );

        await sut.StartAsync(cancellationToken: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 200);
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task StaleCache_BusyProbe_DefersCalibration()
    {
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(expression: b => b.NeedsRecalibration()).Returns(value: true);
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new SpeedIndex(Measurements: new()));

        Mock<IEncoderActivityProbe> probe = new();
        probe.Setup(expression: p => p.IsBusy).Returns(value: true);

        HardwareBenchmarkBackgroundService sut = NewService(
            benchmark: benchmark.Object,
            autoCalibrate: true,
            startedNow: true,
            probe: probe.Object
        );

        await sut.StartAsync(cancellationToken: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 300);
        await sut.StopAsync(cancellationToken: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task StaleCache_IdleProbe_RunsCalibration()
    {
        TaskCompletionSource<bool> calibrated = new();
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(expression: b => b.NeedsRecalibration()).Returns(value: true);
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(
                valueFunction: (CancellationToken _) =>
                {
                    calibrated.TrySetResult(result: true);
                    return Task.FromResult(result: new SpeedIndex(Measurements: new()));
                }
            );

        HardwareBenchmarkBackgroundService sut = NewService(
            benchmark: benchmark.Object,
            autoCalibrate: true,
            startedNow: true
        );

        await sut.StartAsync(cancellationToken: CancellationToken.None);

        bool ran = await Task.WhenAny(task1: calibrated.Task, task2: Task.Delay(millisecondsDelay: 2000)) == calibrated.Task;
        ran.Should().BeTrue(because: "benchmark should run once probe reports idle and grace elapses");

        await sut.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task NotYetStarted_WaitsForApplicationStarted()
    {
        TaskCompletionSource<bool> calibrated = new();
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(expression: b => b.NeedsRecalibration()).Returns(value: true);
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(
                valueFunction: (CancellationToken _) =>
                {
                    calibrated.TrySetResult(result: true);
                    return Task.FromResult(result: new SpeedIndex(Measurements: new()));
                }
            );

        ControllableLifetime lifetime = new();
        HardwareBenchmarkBackgroundService sut = NewService(
            benchmark: benchmark.Object,
            autoCalibrate: true,
            startedNow: false,
            lifetime: lifetime
        );

        await sut.StartAsync(cancellationToken: CancellationToken.None);

        // Benchmark must not have run yet — ApplicationStarted hasn't fired.
        bool ranTooEarly = await Task.WhenAny(task1: calibrated.Task, task2: Task.Delay(millisecondsDelay: 150)) == calibrated.Task;
        ranTooEarly.Should().BeFalse(because: "benchmark must wait for ApplicationStarted");

        lifetime.SignalStarted();
        bool ranAfterStart =
            await Task.WhenAny(task1: calibrated.Task, task2: Task.Delay(millisecondsDelay: 2000)) == calibrated.Task;
        ranAfterStart.Should().BeTrue(because: "benchmark should run after ApplicationStarted fires");

        await sut.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task PendingBinariesStage_DefersCalibrationUntilBinariesComplete()
    {
        TaskCompletionSource<bool> calibrated = new();
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(expression: b => b.NeedsRecalibration()).Returns(value: true);
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(
                valueFunction: (CancellationToken _) =>
                {
                    calibrated.TrySetResult(result: true);
                    return Task.FromResult(result: new SpeedIndex(Measurements: new()));
                }
            );

        // Fresh tracker with no stages marked — ffmpeg is not on disk yet.
        ServerPhaseTracker tracker = new();

        HardwareBenchmarkBackgroundService sut = NewService(
            benchmark: benchmark.Object,
            autoCalibrate: true,
            startedNow: true,
            phaseTracker: tracker
        );

        await sut.StartAsync(cancellationToken: CancellationToken.None);

        bool ranTooEarly = await Task.WhenAny(task1: calibrated.Task, task2: Task.Delay(millisecondsDelay: 200)) == calibrated.Task;
        ranTooEarly.Should().BeFalse(because: "benchmark must not spawn ffmpeg before BootStage.Binaries");

        tracker.MarkComplete(stage: BootStage.Binaries);

        bool ranAfter = await Task.WhenAny(task1: calibrated.Task, task2: Task.Delay(millisecondsDelay: 2000)) == calibrated.Task;
        ranAfter.Should().BeTrue(because: "benchmark should run once binaries are provisioned");

        await sut.StopAsync(cancellationToken: CancellationToken.None);
    }

    private static HardwareBenchmarkBackgroundService NewService(
        IHardwareBenchmark benchmark,
        bool autoCalibrate,
        bool startedNow,
        IEncoderActivityProbe? probe = null,
        ControllableLifetime? lifetime = null,
        IServerPhaseTracker? phaseTracker = null
    )
    {
        ControllableLifetime effectiveLifetime = lifetime ?? new ControllableLifetime();
        if (startedNow)
            effectiveLifetime.SignalStarted();

        Mock<IEncoderActivityProbe> defaultProbe = new();
        defaultProbe.Setup(expression: p => p.IsBusy).Returns(value: false);

        return new(
            benchmark: benchmark,
            options: new()
            {
                FfmpegPathOverride = "ffmpeg",
                FfprobePathOverride = "ffprobe",
                AutoCalibrate = autoCalibrate,
            },
            lifetime: effectiveLifetime,
            activityProbe: probe ?? defaultProbe.Object,
            logger: NullLogger<HardwareBenchmarkBackgroundService>.Instance,
            initialGrace: TimeSpan.Zero,
            busyPollInterval: TimeSpan.FromMilliseconds(milliseconds: 20),
            phaseTracker: phaseTracker
        );
    }

    private sealed class ControllableLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;

        public void SignalStarted()
        {
            if (!_started.IsCancellationRequested)
                _started.Cancel();
        }

        public void StopApplication()
        {
            if (!_stopping.IsCancellationRequested)
                _stopping.Cancel();
            if (!_stopped.IsCancellationRequested)
                _stopped.Cancel();
        }

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
