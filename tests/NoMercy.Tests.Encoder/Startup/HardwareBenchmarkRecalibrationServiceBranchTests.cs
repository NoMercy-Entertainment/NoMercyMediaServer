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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Startup;

namespace NoMercy.Tests.Encoder.Startup;

/// <summary>
/// Branch-coverage gaps not reached by
/// <see cref="HardwareBenchmarkRecalibrationServiceTests"/>:
///
/// • AutoCalibrate=false — ExecuteAsync returns immediately, no timer set up.
/// • LastCalibratedAt=null — first-ever boot path; IsAgeExceeded returns true
///   so recalibration triggers without a known prior timestamp.
/// • DriverChangeDetector throws — error is swallowed, treated as "no change".
///   The recalibrator must NEVER crash from a faulty driver-fingerprint backend.
/// • CalibrateAsync throws — exception logged, returned false from
///   WaitForIdleAndRunAsync, no propagation to caller.
/// • CalibrateAsync OperationCanceledException — surfaced as "cancelled" log
///   and false return, NOT propagated as an exception.
/// • WaitForIdleAndRunAsync cancellation during deferral Task.Delay returns
///   false without throwing.
/// • Cancellation token pre-checked at EvaluateAndRecalibrateAsync entry.
/// </summary>
public class HardwareBenchmarkRecalibrationServiceBranchTests
{
    // ── AutoCalibrate=false ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_returns_immediately_when_AutoCalibrate_is_false()
    {
        // BackgroundService surface — ExecuteAsync is protected. We test the
        // dormant contract: when AutoCalibrate=false, the service must NOT
        // hit DetectAndPersistAsync (which it would on the first timer tick).
        Mock<IDriverChangeDetector> driverDetector = new();
        Mock<IHardwareBenchmark> benchmark = new();
        Mock<ISpeedIndexStore> store = new();
        Mock<IEncoderActivityProbe> activityProbe = new();

        HardwareBenchmarkRecalibrationService sut = new(
            benchmark: benchmark.Object,
            driverChangeDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object,
            options: new() { AutoCalibrate = false },
            logger: NullLogger<HardwareBenchmarkRecalibrationService>.Instance,
            checkInterval: TimeSpan.FromMilliseconds(milliseconds: 1),
            busyRetryInterval: TimeSpan.FromMilliseconds(milliseconds: 1),
            maxDeferralWindow: TimeSpan.Zero
        );

        // Drive ExecuteAsync via StartAsync + StopAsync. The CT we pass to
        // StopAsync is independent of the internal stoppingToken — we just
        // want to confirm nothing happened.
        await ((IHostedService)sut).StartAsync(cancellationToken: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50);
        await ((IHostedService)sut).StopAsync(cancellationToken: CancellationToken.None);

        driverDetector.Verify(
            expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()),
            times: Times.Never
        );
        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    // ── First-boot — LastCalibratedAt is null ────────────────────────────────

    [Fact]
    public async Task LastCalibratedAt_null_triggers_recalibration_as_if_age_exceeded()
    {
        // First-ever boot: store has no prior timestamp. The "fresh" check
        // must NOT silently no-op — IsAgeExceeded returns true so recal fires.
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: (DateTime?)null);

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "x",
                    PreviousHash: "x",
                    Changed: false,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: false);

        Mock<IHardwareBenchmark> benchmark = new();
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new SpeedIndex(Measurements: []));

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        await sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Once);
    }

    // ── DriverChangeDetector throws → treated as unchanged ───────────────────

    [Fact]
    public async Task DriverChangeDetector_throws_is_swallowed_and_treated_as_unchanged()
    {
        // The fingerprint backend can fail (WMI hiccup, sysfs permission error)
        // — must NOT crash the recalibration service. Combined with a fresh
        // store, the failure should result in NO recalibration call.
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -1)); // fresh

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new InvalidOperationException(message: "driver fingerprint failed"));

        Mock<IEncoderActivityProbe> activityProbe = new();
        Mock<IHardwareBenchmark> benchmark = new();

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        // Must not throw, and must not call CalibrateAsync since store is fresh + driver "unchanged".
        Func<Task> act = () => sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);
        await act.Should().NotThrowAsync();
        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task DriverChangeDetector_cancellation_propagates_out()
    {
        // The catch filter explicitly excludes OperationCanceledException —
        // a cancellation during driver detection should bubble up so the
        // outer ExecuteAsync loop sees it.
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -1));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new OperationCanceledException());

        Mock<IHardwareBenchmark> benchmark = new();
        Mock<IEncoderActivityProbe> activityProbe = new();

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        Func<Task> act = () => sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── CalibrateAsync throws ────────────────────────────────────────────────

    [Fact]
    public async Task CalibrateAsync_throws_is_logged_and_does_not_propagate()
    {
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -31));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "x",
                    PreviousHash: "x",
                    Changed: false,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: false);

        Mock<IHardwareBenchmark> benchmark = new();
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new InvalidOperationException(message: "benchmark exploded"));

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        Func<Task> act = () => sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);
        await act.Should().NotThrowAsync();
        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Once);
    }

    [Fact]
    public async Task CalibrateAsync_OperationCanceledException_does_not_propagate()
    {
        // Different from a general exception — the OperationCanceledException
        // arm explicitly logs "cancelled" and returns false.
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -31));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "x",
                    PreviousHash: "x",
                    Changed: false,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: false);

        Mock<IHardwareBenchmark> benchmark = new();
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new OperationCanceledException());

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        Func<Task> act = () => sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ── WaitForIdleAndRunAsync cancellation during deferral ──────────────────

    [Fact]
    public async Task Idle_wait_cancellation_during_deferral_returns_without_calibrating()
    {
        // Busy encoder + non-zero max-deferral window. Cancellation during
        // Task.Delay must return cleanly without calling CalibrateAsync.
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -31));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "x",
                    PreviousHash: "x",
                    Changed: false,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: true); // always busy

        Mock<IHardwareBenchmark> benchmark = new();

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object,
            busyRetryInterval: TimeSpan.FromSeconds(seconds: 10), // long retry to force delay
            maxDeferralWindow: TimeSpan.FromMinutes(minutes: 10) // doesn't elapse
        );

        CancellationTokenSource cts = new();
        Task evaluateTask = sut.EvaluateAndRecalibrateAsync(ct: cts.Token);
        await Task.Delay(millisecondsDelay: 50);
        await cts.CancelAsync();

        Func<Task> act = () => evaluateTask;
        await act.Should().NotThrowAsync();
        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    // ── Cancellation pre-check ───────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAndRecalibrateAsync_with_pre_cancelled_token_returns_immediately()
    {
        // Top-of-method `if (ct.IsCancellationRequested) return;` short-circuit.
        Mock<ISpeedIndexStore> store = new();
        Mock<IDriverChangeDetector> driverDetector = new();
        Mock<IEncoderActivityProbe> activityProbe = new();
        Mock<IHardwareBenchmark> benchmark = new();

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await sut.EvaluateAndRecalibrateAsync(ct: cts.Token);

        // Pre-cancelled — nothing should have been called.
        driverDetector.Verify(
            expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()),
            times: Times.Never
        );
        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HardwareBenchmarkRecalibrationService BuildService(
        IHardwareBenchmark benchmark,
        IDriverChangeDetector driverDetector,
        ISpeedIndexStore store,
        IEncoderActivityProbe activityProbe,
        TimeSpan? busyRetryInterval = null,
        TimeSpan? maxDeferralWindow = null
    ) =>
        new(
            benchmark: benchmark,
            driverChangeDetector: driverDetector,
            store: store,
            activityProbe: activityProbe,
            options: new() { AutoCalibrate = true },
            logger: NullLogger<HardwareBenchmarkRecalibrationService>.Instance,
            checkInterval: TimeSpan.FromMilliseconds(milliseconds: 1),
            busyRetryInterval: busyRetryInterval ?? TimeSpan.FromMilliseconds(milliseconds: 1),
            maxDeferralWindow: maxDeferralWindow
                ?? HardwareBenchmarkRecalibrationService.DefaultMaxDeferralWindow
        );
}
