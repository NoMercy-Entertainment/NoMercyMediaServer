// Copyright (c) 2024-2026 NoMercy Entertainment. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Startup;
using NoMercy.NmSystem.Lifecycle;

namespace NoMercy.Tests.Encoder.Startup;

public class HardwareInitializationServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Mock<IProcessRunner> BuildProcessRunnerSuccess()
    {
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));
        return processRunner;
    }

    /// <summary>
    /// Creates a <see cref="ServerPhaseTracker"/> with <see cref="BootStage.Binaries"/>
    /// already marked complete so HIS does not block in tests that do not
    /// care about the gate timing.
    /// </summary>
    private static ServerPhaseTracker BinariesReadyTracker()
    {
        ServerPhaseTracker tracker = new();
        tracker.MarkComplete(BootStage.Binaries);
        return tracker;
    }

    // -------------------------------------------------------------------------
    // Core detection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_DetectsHardware_SetsCapabilities()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            BinariesReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.CpuCores.Should().Be(4);
        service.Capabilities.HasGpu.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_IsNotReadyBeforeStart()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            BinariesReadyTracker(),
            probeRetryDelayMs: 0
        );

        service.IsReady.Should().BeFalse();

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_OnDetectionFailure_FallsBackToSoftware()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GPU probe exploded"));

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            BinariesReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.HasGpu.Should().BeFalse();
        service.Capabilities.CpuCores.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public async Task StopAsync_CompletesImmediately()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            BinariesReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;
        await service.StopAsync(CancellationToken.None);

        service.IsReady.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Boot-gate regression tests (the intermittent race fix)
    // -------------------------------------------------------------------------

    /// <summary>
    /// StartAsync must return Task.CompletedTask immediately — it must not
    /// block waiting for binaries or hardware detection. If this test hangs
    /// it means the gate was moved back into StartAsync (deadlock risk).
    /// </summary>
    [Fact]
    public async Task StartAsync_ReturnsImmediately_WithoutAwaitingDetection()
    {
        // Binaries stage is NOT yet marked — detection would block indefinitely
        // if StartAsync awaited it directly.
        ServerPhaseTracker tracker = new();

        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            tracker,
            probeRetryDelayMs: 0
        );

        // StartAsync must complete before we signal Binaries — if it blocks
        // internally the test would deadlock here (caught by xUnit timeout).
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Service is not yet ready — detection is gated on Binaries.
        service.IsReady.Should().BeFalse();

        // Now unblock the gate and let detection finish.
        tracker.MarkComplete(BootStage.Binaries);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
    }

    /// <summary>
    /// When the probe returns an empty encoder list (simulating a transiently
    /// locked / mid-replace binary), HIS retries and succeeds on a later attempt.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenProbeReturnsEmptyThenSucceeds_RetriesAndSetsGpu()
    {
        // First two probe calls return empty output; third returns encoders.
        string encoderOutput =
            "V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)";
        int callCount = 0;
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(a => a.Length == 1 && a[0] == "-encoders"),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                string stdout = callCount <= 2 ? "" : encoderOutput;
                return new ProcessResult(0, stdout, "", TimeSpan.Zero);
            });
        processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(a => a.Length == 1 && a[0] != "-encoders"),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));

        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(processRunner.Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            BinariesReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
        // Encoder list was populated on the third attempt — not stuck on empty.
        ffmpegCaps.AvailableEncoders.Should().Contain("h264_nvenc");
    }

    /// <summary>
    /// When every probe attempt returns an empty encoder list (genuine
    /// software-only host), HIS still completes and sets CPU-only capabilities
    /// after the bounded retries.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenProbeAlwaysEmpty_FallsBackToCpuOnly()
    {
        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Environment.ProcessorCount);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            BinariesReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.HasGpu.Should().BeFalse();
    }

    /// <summary>
    /// When the ffmpeg binary itself fails (non-zero exit), ProbeListAsync throws
    /// and the HIS catch block falls back to CPU-only — not silently stuck on empty.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenProbeFails_FallsBackToCpuOnly()
    {
        Mock<IProcessRunner> failingRunner = new();
        failingRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(1, "", "ffmpeg: command not found", TimeSpan.Zero));

        FfmpegCapabilities ffmpegCaps = new(failingRunner.Object);

        Mock<IHardwareDetector> detector = new();

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new HardwareCapabilitiesHolder(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            BinariesReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.HasGpu.Should().BeFalse();
        service.Capabilities.CpuCores.Should().Be(Environment.ProcessorCount);
    }
}
