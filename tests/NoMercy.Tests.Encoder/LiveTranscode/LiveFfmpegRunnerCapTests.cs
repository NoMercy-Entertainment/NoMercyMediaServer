using FluentAssertions.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Resources;
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
        IStorage? storage = null
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

        return new LiveFfmpegRunner(
            processRunner ?? MakeInstantProcessRunner(),
            opts,
            NullLogger<LiveFfmpegRunner>.Instance,
            storage ?? TestStorageFactory.CreateLocal(),
            cap,
            hw,
            noopBudget.Object
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Hardware encode — cap exhausted → throws EncoderRuntimeException 409
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_HwQuality_CapExhausted_ThrowsEncoderRuntimeException()
    {
        Mock<INvencSessionCap> capMock = new();
        capMock
            .Setup(c => c.EnforceForGpuEncode(It.IsAny<string>(), true))
            .Throws(RuntimeErrors.GpuCapacityExhausted("RTX 3080", 3));

        LiveFfmpegRunner runner = BuildRunner(capMock.Object);
        LiveSession session = new("cap-hw-001", MakeHwQuality());
        string outputDir = Path.Combine(Path.GetTempPath(), "nomercy-cap-test-" + Ulid.NewUlid());

        Func<Task> act = () =>
            runner.RunAsync(MakeInput(MakeHwQuality(), outputDir), session, CancellationToken.None);

        ExceptionAssertions<EncoderRuntimeException> ex = await act.Should()
            .ThrowAsync<EncoderRuntimeException>();

        ex.Which.Shape.Id.Should().Be(EncoderRuleId.GpuCapacityExhausted);
        ex.Which.HttpStatusCode.Should().Be(409);
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
