namespace NoMercy.Tests.Encoder.Hardware;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;

public class HardwareBenchmarkTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // BuildCalibrationArguments
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildCalibrationArguments_IncludesLavfiTestSource()
    {
        EncoderInfo encoder = MakeSoftwareH264();

        string[] args = HardwareBenchmark.BuildCalibrationArguments(encoder, 1920, 1080);

        int fIdx = Array.IndexOf(args, "-f");
        args[fIdx + 1].Should().Be("lavfi");

        int iIdx = Array.IndexOf(args, "-i");
        args[iIdx + 1].Should().Contain("testsrc=");
        args[iIdx + 1].Should().Contain("size=1920x1080");
        args[iIdx + 1].Should().Contain("rate=30");
    }

    [Fact]
    public void BuildCalibrationArguments_UsesNullMuxerSoNothingIsWritten()
    {
        string[] args = HardwareBenchmark.BuildCalibrationArguments(MakeSoftwareH264(), 1280, 720);

        // Last output should be "-f null -" to discard encoded frames.
        int nullIdx = -1;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-f" && args[i + 1] == "null")
            {
                nullIdx = i;
                break;
            }
        }

        // lavfi input comes first, null output comes second — must find two "-f" occurrences
        int firstF = Array.IndexOf(args, "-f");
        int secondF = Array.IndexOf(args, "-f", firstF + 1);
        secondF.Should().BeGreaterThan(firstF);
        args[secondF + 1].Should().Be("null");
    }

    [Fact]
    public void BuildCalibrationArguments_SelectsMediumPresetForSoftware()
    {
        string[] args = HardwareBenchmark.BuildCalibrationArguments(MakeSoftwareH264(), 1280, 720);

        int presetIdx = Array.IndexOf(args, "-preset");
        args[presetIdx + 1].Should().Be("medium");
    }

    [Fact]
    public void BuildCalibrationArguments_SelectsP4PresetForNvenc()
    {
        EncoderInfo nvenc = new(
            FfmpegName: "h264_nvenc",
            RequiredVendor: GpuVendor.Nvidia,
            Presets: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"],
            Profiles: ["high"],
            Levels: [],
            QualityRange: new QualityRange(0, 51, 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 12,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new Dictionary<string, string>()
        );

        string[] args = HardwareBenchmark.BuildCalibrationArguments(nvenc, 1920, 1080);

        int presetIdx = Array.IndexOf(args, "-preset");
        args[presetIdx + 1].Should().Be("p4");
    }

    [Fact]
    public void BuildCalibrationArguments_EmitsVendorSpecificFlags()
    {
        EncoderInfo amf = new(
            FfmpegName: "h264_amf",
            RequiredVendor: GpuVendor.Amd,
            Presets: ["balanced"],
            Profiles: ["high"],
            Levels: [],
            QualityRange: new QualityRange(0, 51, 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new Dictionary<string, string> { ["-usage"] = "transcoding" }
        );

        string[] args = HardwareBenchmark.BuildCalibrationArguments(amf, 1280, 720);

        int usageIdx = Array.IndexOf(args, "-usage");
        usageIdx.Should().BeGreaterThan(-1);
        args[usageIdx + 1].Should().Be("transcoding");
    }

    [Fact]
    public void BuildCalibrationArguments_IncludesProgressPipe()
    {
        string[] args = HardwareBenchmark.BuildCalibrationArguments(MakeSoftwareH264(), 1920, 1080);

        int idx = Array.IndexOf(args, "-progress");
        args[idx + 1].Should().Be("pipe:1");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SelectCandidates
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SelectCandidates_ExcludesHwEncodersForMissingVendor()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(h => h.Gpus).Returns([]); // no GPUs installed

        HardwareBenchmark sut = NewBenchmark(hardware.Object);

        List<EncoderInfo> candidates = sut.SelectCandidates().Select(c => c.Encoder).ToList();

        // Software encoders (no RequiredVendor) must remain.
        candidates.Should().Contain(e => e.FfmpegName == "libx264");
        // Hardware encoders must be filtered out.
        candidates.Should().NotContain(e => e.FfmpegName == "h264_nvenc");
        candidates.Should().NotContain(e => e.FfmpegName == "h264_amf");
        candidates.Should().NotContain(e => e.FfmpegName == "h264_qsv");
    }

    [Fact]
    public void SelectCandidates_IncludesHwEncodersForPresentVendor()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware
            .Setup(h => h.Gpus)
            .Returns([
                new GpuDevice(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 4080",
                    VramMb: 16_384,
                    MaxEncoderSessions: 12,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
                ),
            ]);

        HardwareBenchmark sut = NewBenchmark(hardware.Object);

        List<string> names = sut.SelectCandidates().Select(c => c.Encoder.FfmpegName).ToList();

        names.Should().Contain("h264_nvenc");
        names.Should().NotContain("h264_amf");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalibrateAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalibrateAsync_PopulatesIndexFromProgressOutput()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>>(),
                    It.IsAny<Action<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    string[] _,
                    Action<string>? onStdOut,
                    Action<string>? _,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    // Feed a typical ffmpeg -progress payload: 60 fps, end marker.
                    onStdOut?.Invoke("frame=150");
                    onStdOut?.Invoke("fps=60");
                    onStdOut?.Invoke("speed=2.0x");
                    onStdOut?.Invoke("progress=end");
                    return Task.FromResult(
                        new ProcessResult(
                            ExitCode: 0,
                            StdOut: "",
                            StdErr: "",
                            Duration: TimeSpan.Zero
                        )
                    );
                }
            );

        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(h => h.Gpus).Returns([]);

        Mock<ISpeedIndexStore> store = new();

        HardwareBenchmark sut = NewBenchmark(hardware.Object, runner.Object, store.Object);

        SpeedIndex index = await sut.CalibrateAsync(CancellationToken.None);

        index.Measurements.Should().NotBeEmpty();
        index.Measurements.Values.Should().AllSatisfy(m => m.Fps.Should().Be(60));
        index.Measurements.Values.Should().AllSatisfy(m => m.SpeedMultiplier.Should().Be(2.0));
        store.Verify(s => s.Save(It.IsAny<SpeedIndex>()), Times.Once);
    }

    [Fact]
    public async Task CalibrateAsync_SkipsFailedEncoderRuns()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>>(),
                    It.IsAny<Action<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                Task.FromResult(
                    new ProcessResult(
                        ExitCode: 1,
                        StdOut: "",
                        StdErr: "boom",
                        Duration: TimeSpan.Zero
                    )
                )
            );

        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(h => h.Gpus).Returns([]);
        Mock<ISpeedIndexStore> store = new();

        HardwareBenchmark sut = NewBenchmark(hardware.Object, runner.Object, store.Object);

        SpeedIndex index = await sut.CalibrateAsync(CancellationToken.None);

        index.Measurements.Should().BeEmpty();
    }

    [Fact]
    public void NeedsRecalibration_EmptyCache_ReturnsTrue()
    {
        Mock<ISpeedIndexStore> store = new();
        store.Setup(s => s.Load()).Returns((SpeedIndex?)null);
        store.Setup(s => s.LastCalibratedAt).Returns((DateTime?)null);

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeTrue();
    }

    [Fact]
    public void NeedsRecalibration_OldCache_ReturnsTrue()
    {
        Mock<ISpeedIndexStore> store = new();
        SpeedIndex index = new(
            new Dictionary<SpeedKey, SpeedMeasurement>
            {
                [new SpeedKey(VideoCodecType.H264, "libx264", 1920, null)] = new(
                    60,
                    2.0,
                    DateTime.UtcNow.AddDays(-60)
                ),
            }
        );
        store.Setup(s => s.Load()).Returns(index);
        store.Setup(s => s.LastCalibratedAt).Returns(DateTime.UtcNow.AddDays(-60));

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeTrue();
    }

    [Fact]
    public void NeedsRecalibration_FreshCache_ReturnsFalse()
    {
        Mock<ISpeedIndexStore> store = new();
        SpeedIndex index = new(
            new Dictionary<SpeedKey, SpeedMeasurement>
            {
                [new SpeedKey(VideoCodecType.H264, "libx264", 1920, null)] = new(
                    60,
                    2.0,
                    DateTime.UtcNow.AddDays(-1)
                ),
            }
        );
        store.Setup(s => s.Load()).Returns(index);
        store.Setup(s => s.LastCalibratedAt).Returns(DateTime.UtcNow.AddDays(-1));

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static HardwareBenchmark NewBenchmark(
        IHardwareCapabilities? hardware = null,
        IProcessRunner? processRunner = null,
        ISpeedIndexStore? store = null
    )
    {
        Mock<IHardwareCapabilities> hw = new();
        hw.Setup(h => h.Gpus).Returns([]);
        IHardwareCapabilities hwImpl = hardware ?? hw.Object;

        IProcessRunner runner =
            processRunner ?? new Mock<IProcessRunner>(MockBehavior.Loose).Object;
        ISpeedIndexStore storeImpl = store ?? new Mock<ISpeedIndexStore>().Object;

        return new HardwareBenchmark(
            new CodecRegistry(),
            hwImpl,
            runner,
            storeImpl,
            new EncoderOptions { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            NullLogger<HardwareBenchmark>.Instance
        );
    }

    private static EncoderInfo MakeSoftwareH264() =>
        new(
            FfmpegName: "libx264",
            RequiredVendor: null,
            Presets: ["ultrafast", "fast", "medium", "slow", "veryslow"],
            Profiles: ["high"],
            Levels: ["4.1"],
            QualityRange: new QualityRange(0, 51, 23),
            SupportedRateControl: [RateControlMode.Crf],
            Supports10Bit: true,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "yuv420p10le",
            VendorSpecificFlags: new Dictionary<string, string>()
        );
}
