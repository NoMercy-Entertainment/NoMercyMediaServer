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
            "1080p",
            "1080p",
            1920,
            1080,
            VideoCodecType.H264,
            8000,
            "h264_nvenc",
            true,
            5.0,
            true
        );

    private static LiveQuality MakeSwQuality() =>
        new(
            "1080p-sw",
            "1080p",
            1920,
            1080,
            VideoCodecType.H264,
            8000,
            "libx264",
            false,
            2.0,
            true
        );

    private static LiveRunInput MakeInput(LiveQuality quality, string outputDir) =>
        new(
            "/media/test.mkv",
            outputDir,
            TimeSpan.Zero,
            quality,
            4
        );

    /// <summary>
    /// Builds a process runner mock that returns a successful result immediately.
    /// </summary>
    private static IProcessRunner MakeInstantProcessRunner()
    {
        ProcessResult ok = new(0, "", "", TimeSpan.Zero);
        Mock<IProcessRunner> mock = new();

        mock.Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(ok);

        return mock.Object;
    }

    /// <summary>
    /// Builds a storage mock that satisfies the AcquireLocalPath + CreateDirectory calls
    /// made before FFmpeg is spawned, and returns "no playlist exists" so polling exits cleanly.
    /// </summary>
    private static IStorage MakeNoopStorage()
    {
        Mock<IStorage> mock = new();
        mock.Setup(s => s.CreateDirectory(It.IsAny<string>()));
        mock.Setup(s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns(new LocalPathLease(Path.GetTempPath()));
        mock.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);
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
            hardware ?? new HardwareCapabilities([], Environment.ProcessorCount);

        EncoderOptions opts = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };

        Mock<IResourceBudget> noopBudget = new();
        ResourceLease noopLease = new("noop", null, 0, 0);
        noopBudget.Setup(b => b.Acquire(It.IsAny<ResourceRequirement>())).Returns(noopLease);
        noopBudget
            .Setup(b =>
                b.AcquireAsync(It.IsAny<ResourceRequirement>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(noopLease);
        noopBudget.Setup(b => b.Release(It.IsAny<ResourceLease>()));

        return new(
            processRunner ?? MakeInstantProcessRunner(),
            opts,
            NullLogger<LiveFfmpegRunner>.Instance,
            storage ?? TestStorageFactory.CreateLocal(),
            cap,
            hw,
            codecResolver ?? new CodecResolver(new CodecRegistry()),
            noopBudget.Object,
            transport
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
            .Setup(c => c.EnforceForGpuEncode(It.IsAny<string>(), true))
            .Throws(RuntimeErrors.GpuCapacityExhausted("RTX 3080", 3));

        List<(string SessionId, object Message)> pushed = [];
        Mock<ILiveSessionTransport> transportMock = new();
        transportMock
            .Setup(t =>
                t.SendToClientAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, object, CancellationToken>(
                (sessionId, message, _) => pushed.Add((sessionId, message))
            )
            .Returns(Task.CompletedTask);

        LiveFfmpegRunner runner = BuildRunner(
            capMock.Object,
            processRunner: MakeInstantProcessRunner(),
            storage: MakeNoopStorage(),
            transport: transportMock.Object
        );

        LiveQuality hwQuality = MakeHwQuality();
        LiveSession session = new("cap-hw-001", hwQuality);
        string outputDir = Path.Combine(Path.GetTempPath(), "nomercy-cap-test-" + Ulid.NewUlid());

        Func<Task> act = () =>
            runner.RunAsync(MakeInput(hwQuality, outputDir), session, CancellationToken.None);

        await act.Should().NotThrowAsync();

        session.CurrentQuality.Encoder.Should().Be("libx264");
        session.CurrentQuality.IsHardwareAccelerated.Should().BeFalse();

        pushed.Should().ContainSingle(p => p.Message is QualityChangedMessage);
        QualityChangedMessage message = pushed
            .Select(p => p.Message)
            .OfType<QualityChangedMessage>()
            .Single();
        message.Reason.Should().Be(QualityChangeReason.GpuFallbackToCpu);
        message.NewQuality.Encoder.Should().Be("libx264");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Software encode — EnforceForGpuEncode called with requiresGpu=false
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SwQuality_EnforceCalledWithRequiresGpuFalse()
    {
        Mock<INvencSessionCap> capMock = new();

        LiveFfmpegRunner runner = BuildRunner(
            capMock.Object,
            processRunner: MakeInstantProcessRunner(),
            storage: MakeNoopStorage()
        );

        LiveSession session = new("cap-sw-001", MakeSwQuality());
        string outputDir = Path.Combine(Path.GetTempPath(), "cap-sw-" + Ulid.NewUlid());

        await runner.RunAsync(
            MakeInput(MakeSwQuality(), outputDir),
            session,
            CancellationToken.None
        );

        // Must be called with requiresGpu=false for software encodes.
        capMock.Verify(c => c.EnforceForGpuEncode(It.IsAny<string>(), false), Times.Once);
        capMock.Verify(c => c.EnforceForGpuEncode(It.IsAny<string>(), true), Times.Never);
    }
}
