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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.LiveTranscode.Protocol;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveFfmpegRunnerCapTests
{
    private static LiveQuality MakeHwQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "h264_nvenc",
            IsHardwareAccelerated: true,
            ExpectedSpeed: 5.0,
            CanRealtime: true
        );

    private static LiveQuality MakeSwQuality() =>
        new(
            Id: "1080p-sw",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    private static LiveRunInput MakeInput(LiveQuality quality, string outputDir) =>
        new(
            InputPath: "/media/test.mkv",
            OutputDirectory: outputDir,
            StartPosition: TimeSpan.Zero,
            Quality: quality,
            SegmentDurationSeconds: 4
        );

    /// <summary>
    /// Builds a process runner mock that returns a successful result immediately.
    /// </summary>
    private static IProcessRunner MakeInstantProcessRunner()
    {
        ProcessResult ok = new(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero);
        Mock<IProcessRunner> mock = new();

        mock.Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: ok);

        return mock.Object;
    }

    /// <summary>
    /// Builds a storage mock that satisfies the AcquireLocalPath + CreateDirectory calls
    /// made before FFmpeg is spawned, and returns "no playlist exists" so polling exits cleanly.
    /// </summary>
    private static IStorage MakeNoopStorage()
    {
        Mock<IStorage> mock = new();
        mock.Setup(expression: s => s.CreateDirectory(It.IsAny<string>()));
        mock.Setup(expression: s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns(value: new LocalPathLease(path: Path.GetTempPath()));
        mock.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: false);
        return mock.Object;
    }

    private static LiveFfmpegRunner BuildRunner(
        INvencSessionCap cap,
        IHardwareCapabilities? hardware = null,
        IProcessRunner? processRunner = null,
        IStorage? storage = null,
        ICodecResolver? codecResolver = null,
        ILiveSessionTransport? transport = null
    )
    {
        IHardwareCapabilities hw =
            hardware ?? new HardwareCapabilities(Gpus: [], CpuCores: Environment.ProcessorCount);

        EncoderOptions opts = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };

        Mock<IResourceBudget> noopBudget = new();
        ResourceLease noopLease = new(LeaseId: "noop", GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 0);
        noopBudget.Setup(expression: b => b.Acquire(It.IsAny<ResourceRequirement>())).Returns(value: noopLease);
        noopBudget
            .Setup(expression: b =>
                b.AcquireAsync(It.IsAny<ResourceRequirement>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: noopLease);
        noopBudget.Setup(expression: b => b.Release(It.IsAny<ResourceLease>()));

        return new(
            processRunner: processRunner ?? MakeInstantProcessRunner(),
            options: opts,
            logger: NullLogger<LiveFfmpegRunner>.Instance,
            storage: storage ?? TestStorageFactory.CreateLocal(),
            nvencSessionCap: cap,
            hardware: hw,
            codecResolver: codecResolver ?? new CodecResolver(registry: new CodecRegistry()),
            resourceBudget: noopBudget.Object,
            transport: transport
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Hardware encode — cap exhausted → falls back to software instead of
    // failing the session, and reports GpuFallbackToCpu on the transport.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_HwQuality_CapExhausted_FallsBackToSoftwareAndReportsGpuFallback()
    {
        Mock<INvencSessionCap> capMock = new();
        capMock
            .Setup(expression: c => c.EnforceForGpuEncode(It.IsAny<string>(), true))
            .Throws(exception: RuntimeErrors.GpuCapacityExhausted(gpu: "RTX 3080", sessions: 3));

        List<(string SessionId, object Message)> pushed = [];
        Mock<ILiveSessionTransport> transportMock = new();
        transportMock
            .Setup(expression: t =>
                t.SendToClientAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, object, CancellationToken>(
                action: (sessionId, message, _) => pushed.Add(item: (sessionId, message))
            )
            .Returns(value: Task.CompletedTask);

        LiveFfmpegRunner runner = BuildRunner(
            cap: capMock.Object,
            processRunner: MakeInstantProcessRunner(),
            storage: MakeNoopStorage(),
            transport: transportMock.Object
        );

        LiveQuality hwQuality = MakeHwQuality();
        LiveSession session = new(sessionId: "cap-hw-001", quality: hwQuality);
        string outputDir = Path.Combine(path1: Path.GetTempPath(), path2: "nomercy-cap-test-" + Ulid.NewUlid());

        Func<Task> act = () =>
            runner.RunAsync(input: MakeInput(quality: hwQuality, outputDir: outputDir), session: session, ct: CancellationToken.None);

        await act.Should().NotThrowAsync();

        session.CurrentQuality.Encoder.Should().Be(expected: "libx264");
        session.CurrentQuality.IsHardwareAccelerated.Should().BeFalse();

        pushed.Should().ContainSingle(predicate: p => p.Message is QualityChangedMessage);
        QualityChangedMessage message = pushed
            .Select(selector: p => p.Message)
            .OfType<QualityChangedMessage>()
            .Single();
        message.Reason.Should().Be(expected: QualityChangeReason.GpuFallbackToCpu);
        message.NewQuality.Encoder.Should().Be(expected: "libx264");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Software encode — EnforceForGpuEncode called with requiresGpu=false
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SwQuality_EnforceCalledWithRequiresGpuFalse()
    {
        Mock<INvencSessionCap> capMock = new();

        LiveFfmpegRunner runner = BuildRunner(
            cap: capMock.Object,
            processRunner: MakeInstantProcessRunner(),
            storage: MakeNoopStorage()
        );

        LiveSession session = new(sessionId: "cap-sw-001", quality: MakeSwQuality());
        string outputDir = Path.Combine(path1: Path.GetTempPath(), path2: "cap-sw-" + Ulid.NewUlid());

        await runner.RunAsync(
            input: MakeInput(quality: MakeSwQuality(), outputDir: outputDir),
            session: session,
            ct: CancellationToken.None
        );

        // Must be called with requiresGpu=false for software encodes.
        capMock.Verify(expression: c => c.EnforceForGpuEncode(It.IsAny<string>(), false), times: Times.Once);
        capMock.Verify(expression: c => c.EnforceForGpuEncode(It.IsAny<string>(), true), times: Times.Never);
    }
}
