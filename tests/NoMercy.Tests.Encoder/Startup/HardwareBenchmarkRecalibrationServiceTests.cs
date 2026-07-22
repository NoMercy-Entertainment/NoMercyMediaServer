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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Startup;

namespace NoMercy.Tests.Encoder.Startup;

public class HardwareBenchmarkRecalibrationServiceTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // RecalAge30DaysTriggersRecal_WhenIdle
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecalAge30DaysTriggersRecal_WhenIdle()
    {
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -31));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "abc",
                    PreviousHash: "abc",
                    Changed: false,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: false);

        Mock<IHardwareBenchmark> benchmark = new();
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new SpeedIndex(Measurements: new()));

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        await sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RecalSkippedWhenBusy
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecalSkippedWhenBusy()
    {
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -31));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "abc",
                    PreviousHash: "abc",
                    Changed: false,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: true);

        Mock<IHardwareBenchmark> benchmark = new();

        // Use a zero max-deferral window so the test doesn't wait 7 days.
        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object,
            maxDeferralWindow: TimeSpan.Zero
        );

        await sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RecalSkippedWhenFresh
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecalSkippedWhenFresh()
    {
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -5));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "same",
                    PreviousHash: "same",
                    Changed: false,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: false);

        Mock<IHardwareBenchmark> benchmark = new();

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        await sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DriverChangeTriggersRecal
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DriverChangeTriggersRecal()
    {
        Mock<ISpeedIndexStore> store = new();
        // Fresh timestamp — age alone would NOT trigger recal.
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -1));

        Mock<IDriverChangeDetector> driverDetector = new();
        driverDetector
            .Setup(expression: d => d.DetectAndPersistAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                value: new DriverChangeResult(
                    CurrentHash: "new-hash",
                    PreviousHash: "old-hash",
                    Changed: true,
                    IsFirstBoot: false
                )
            );

        Mock<IEncoderActivityProbe> activityProbe = new();
        activityProbe.Setup(expression: p => p.IsBusy).Returns(value: false);

        Mock<IHardwareBenchmark> benchmark = new();
        benchmark
            .Setup(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new SpeedIndex(Measurements: new()));

        HardwareBenchmarkRecalibrationService sut = BuildService(
            benchmark: benchmark.Object,
            driverDetector: driverDetector.Object,
            store: store.Object,
            activityProbe: activityProbe.Object
        );

        await sut.EvaluateAndRecalibrateAsync(ct: CancellationToken.None);

        benchmark.Verify(expression: b => b.CalibrateAsync(It.IsAny<CancellationToken>()), times: Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static HardwareBenchmarkRecalibrationService BuildService(
        IHardwareBenchmark benchmark,
        IDriverChangeDetector driverDetector,
        ISpeedIndexStore store,
        IEncoderActivityProbe activityProbe,
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
            busyRetryInterval: TimeSpan.FromMilliseconds(milliseconds: 1),
            maxDeferralWindow: maxDeferralWindow
                ?? HardwareBenchmarkRecalibrationService.DefaultMaxDeferralWindow
        );
}
