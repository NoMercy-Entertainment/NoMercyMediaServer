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
    /// These tests exercise GPU/CPU detection, not the hardware-encoder init
    /// probe — stub it to return an empty usable set explicitly rather than
    /// relying on Moq's default Task-returning behaviour.
    /// </summary>
    private static IHardwareEncoderProbe StubEncoderProbe()
    {
        Mock<IHardwareEncoderProbe> probe = new();
        probe
            .Setup(p =>
                p.ProbeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>());
        return probe.Object;
    }

    /// <summary>
    /// Builds a process runner whose <c>-encoders</c> call returns
    /// <paramref name="encoderOutput"/> and every other single-flag call
    /// (decoders/demuxers/filters/protocols) returns an empty success —
    /// so tests can control exactly which encoder names populate
    /// <see cref="IFfmpegCapabilities.AvailableEncoders"/>.
    /// </summary>
    private static Mock<IProcessRunner> BuildProcessRunnerWithEncoders(string encoderOutput)
    {
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
            .ReturnsAsync(new ProcessResult(0, encoderOutput, "", TimeSpan.Zero));
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
        return processRunner;
    }

    /// <summary>
    /// Creates a <see cref="ServerPhaseTracker"/> with every stage in
    /// <see cref="BootStage.All"/> marked complete so HIS's detection task —
    /// which gates on <see cref="BootStage.All"/> — runs straight through in
    /// tests that do not care about the gate timing. Marking only
    /// <see cref="BootStage.Binaries"/> would leave the await on All hanging
    /// forever (Essential/Auth/Network/Registered never fire in a unit test).
    /// </summary>
    private static ServerPhaseTracker ServerReadyTracker()
    {
        ServerPhaseTracker tracker = new();
        tracker.MarkComplete(BootStage.Essential);
        tracker.MarkComplete(BootStage.Auth);
        tracker.MarkComplete(BootStage.Binaries);
        tracker.MarkComplete(BootStage.Network);
        tracker.MarkComplete(BootStage.Registered);
        return tracker;
    }

    /// <summary>
    /// One detected GPU — the trigger that makes HIS actually run the
    /// hardware-encoder init probe. A zero-GPU host is short-circuited to
    /// software-only before the probe, so any test that asserts on probe
    /// behaviour must supply a detected device.
    /// </summary>
    private static IReadOnlyList<GpuDevice> OneNvidiaGpu() =>
        [new(GpuVendor.Nvidia, "NVIDIA GeForce RTX 4090", 24576, 8, [])];

    // -------------------------------------------------------------------------
    // Core detection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_DetectsHardware_SetsCapabilities()
    {
        Mock<IHardwareDetector> detector = new();
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
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
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
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
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
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
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
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
        // No stage is marked yet — detection gates on BootStage.Binaries and would
        // block indefinitely if StartAsync awaited it directly.
        ServerPhaseTracker tracker = new();

        Mock<IHardwareDetector> detector = new();
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            tracker,
            probeRetryDelayMs: 0
        );

        // StartAsync must complete before we signal Binaries — if it blocks
        // internally the test would deadlock here (caught by xUnit timeout).
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Service is not yet ready — detection is gated on BootStage.Binaries.
        service.IsReady.Should().BeFalse();

        // Now unblock the one stage detection actually waits on and let it finish.
        tracker.MarkComplete(BootStage.Binaries);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
    }

    /// <summary>
    /// Hardware detection must not wait on Auth, Network or Registered — those
    /// describe this server's relationship with nomercy-tv, not whether ffmpeg
    /// is on disk. Regression guard for the boot-stage re-cut: a fresh install
    /// stuck in setup mode (never authenticated, never registered) must still
    /// get GPU detection and let its encoder queues start.
    /// </summary>
    [Fact]
    public async Task StartAsync_DoesNotWaitOnAuthNetworkOrRegistered()
    {
        ServerPhaseTracker tracker = new();
        tracker.MarkComplete(BootStage.Essential);
        tracker.MarkComplete(BootStage.Binaries);

        Mock<IHardwareDetector> detector = new();
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerSuccess().Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            tracker,
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
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
                return new(0, stdout, "", TimeSpan.Zero);
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
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        FfmpegCapabilities ffmpegCaps = new(processRunner.Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
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
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Environment.ProcessorCount);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
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
            StubEncoderProbe(),
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.HasGpu.Should().BeFalse();
        service.Capabilities.CpuCores.Should().Be(Environment.ProcessorCount);
    }

    // -------------------------------------------------------------------------
    // Hardware-encoder init probe wiring
    // -------------------------------------------------------------------------

    /// <summary>
    /// The candidate set handed to <see cref="IHardwareEncoderProbe"/> must be
    /// exactly the compiled hardware-encoder names (those with a known GPU
    /// vendor per <see cref="GpuEncoderTokens.VendorForEncoderName"/>) —
    /// software encoders (libx264, aac, ...) are unconditionally usable and
    /// must never be spent on a process spawn.
    /// </summary>
    [Fact]
    public async Task StartAsync_ProbesOnlyHardwareEncoderNames_NeverSoftware()
    {
        string encoderOutput =
            "V..... libx264              libx264 H.264 (codec h264)\n"
            + "V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)\n"
            + "V..... h264_amf             AMD AMF H.264 encoder (codec h264)\n"
            + "A..... aac                  AAC (Advanced Audio Coding) (codec aac)";

        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerWithEncoders(encoderOutput).Object);

        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OneNvidiaGpu());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        IEnumerable<string>? capturedCandidates = null;
        Mock<IHardwareEncoderProbe> encoderProbe = new();
        encoderProbe
            .Setup(p =>
                p.ProbeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .Callback<IEnumerable<string>, CancellationToken>(
                (candidates, _) => capturedCandidates = [.. candidates]
            )
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>());

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            encoderProbe.Object,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        capturedCandidates.Should().NotBeNull();
        capturedCandidates.Should().Contain(["h264_nvenc", "h264_amf"]);
        capturedCandidates.Should().NotContain(["libx264", "aac"]);
    }

    /// <summary>
    /// A genuinely GPU-less host must never spawn the per-encoder ffmpeg init
    /// probe — <see cref="IHardwareDetector.DetectGpusAsync"/> returning zero
    /// devices is the authoritative "software-only" signal (it already covers
    /// the NVIDIA-in-container case), so probing every compiled-in hardware
    /// encoder only to watch each fail is pure noise and wasted boot time.
    /// Regression guard for the software-only init-failure log spam.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenNoGpuDetected_SkipsHardwareEncoderProbe()
    {
        string encoderOutput =
            "V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)\n"
            + "V..... h264_vaapi           VAAPI H.264 encoder (codec h264)";
        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerWithEncoders(encoderOutput).Object);

        Mock<IHardwareDetector> detector = new();
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        Mock<IHardwareEncoderProbe> encoderProbe = new();
        encoderProbe
            .Setup(p =>
                p.ProbeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>());

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            encoderProbe.Object,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        encoderProbe.Verify(
            p => p.ProbeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.UsableHardwareEncoders.Should().BeEmpty();
        service.Capabilities.HasGpu.Should().BeFalse();
    }

    /// <summary>
    /// The set the probe confirms as usable must land on
    /// <see cref="IHardwareCapabilities.UsableHardwareEncoders"/> — this is
    /// the value <c>PlanStage</c> gates selection on.
    /// </summary>
    [Fact]
    public async Task StartAsync_PopulatesUsableHardwareEncoders_FromProbeResult()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OneNvidiaGpu());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        string encoderOutput =
            "V..... libx264              libx264 H.264 (codec h264)\n"
            + "V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)";
        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerWithEncoders(encoderOutput).Object);

        Mock<IHardwareEncoderProbe> encoderProbe = new();
        encoderProbe
            .Setup(p =>
                p.ProbeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "h264_nvenc" });

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            encoderProbe.Object,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.UsableHardwareEncoders.Should().Contain("h264_nvenc");
    }

    /// <summary>
    /// A probe failure must degrade to "no hardware encoders usable"
    /// (software-only) instead of throwing and taking down hardware
    /// detection entirely — an init-probe outage removes acceleration for
    /// this run, it must never block boot.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenEncoderProbeThrows_DegradesToSoftwareOnly()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OneNvidiaGpu());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        string encoderOutput =
            "V..... libx264              libx264 H.264 (codec h264)\n"
            + "V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)";
        FfmpegCapabilities ffmpegCaps = new(BuildProcessRunnerWithEncoders(encoderOutput).Object);

        Mock<IHardwareEncoderProbe> encoderProbe = new();
        encoderProbe
            .Setup(p =>
                p.ProbeAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("probe exploded"));

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            encoderProbe.Object,
            Mock.Of<IDriverChangeDetector>(),
            Mock.Of<IBenchmarkJobTracker>(),
            new(),
            Mock.Of<ILogger<HardwareInitializationService>>(),
            ServerReadyTracker(),
            probeRetryDelayMs: 0
        );

        await service.StartAsync(CancellationToken.None);
        await service.DetectionTask;

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.UsableHardwareEncoders.Should().BeEmpty();
    }
}
