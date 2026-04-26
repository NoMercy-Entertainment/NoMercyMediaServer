namespace NoMercy.Tests.Encoder.Hardware;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
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

        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            SoftwareTarget(encoder),
            1920,
            1080
        );

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
        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            SoftwareTarget(MakeSoftwareH264()),
            1280,
            720
        );

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
        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            SoftwareTarget(MakeSoftwareH264()),
            1280,
            720
        );

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
            QualityRange: new(0, 51, 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 12,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new()
        );

        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            HardwareTarget(nvenc, "RTX 4080", vendorIndex: 0),
            1920,
            1080
        );

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
            QualityRange: new(0, 51, 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new() { ["-usage"] = "transcoding" }
        );

        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            HardwareTarget(amf, "Radeon 7900XT", vendorIndex: 0),
            1280,
            720
        );

        int usageIdx = Array.IndexOf(args, "-usage");
        usageIdx.Should().BeGreaterThan(-1);
        args[usageIdx + 1].Should().Be("transcoding");
    }

    [Fact]
    public void BuildCalibrationArguments_IncludesProgressPipe()
    {
        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            SoftwareTarget(MakeSoftwareH264()),
            1920,
            1080
        );

        int idx = Array.IndexOf(args, "-progress");
        args[idx + 1].Should().Be("pipe:1");
    }

    [Fact]
    public void BuildCalibrationArguments_CapsFastEncoderFrames_AtSteadyStateLength()
    {
        // libx264 is a fast encoder — gets the long probe (300 frames over
        // ~10s of source) so the steady-state throughput is what gets
        // measured, not first-second spin-up.
        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            SoftwareTarget(MakeSoftwareH264()),
            1920,
            1080
        );

        int framesIdx = Array.IndexOf(args, "-frames:v");
        framesIdx.Should().BeGreaterThan(-1);
        int frameCount = int.Parse(args[framesIdx + 1]);
        frameCount.Should().Be(300);
    }

    [Fact]
    public void BuildCalibrationArguments_CapsSlowEncoderFrames_AtShortProbe()
    {
        // libaom-av1 is in the slow-encoder list — gets the short probe
        // (60 frames) so a single tier doesn't block calibration for
        // multiple minutes.
        EncoderInfo libaom = MakeSoftwareH264() with
        {
            FfmpegName = "libaom-av1",
        };
        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            new CalibrationTarget(VideoCodecType.Av1, libaom, Device: null, VendorIndex: 0),
            1920,
            1080
        );

        int framesIdx = Array.IndexOf(args, "-frames:v");
        framesIdx.Should().BeGreaterThan(-1);
        int frameCount = int.Parse(args[framesIdx + 1]);
        frameCount.Should().Be(60);
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
                new(
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

    [Fact]
    public void SelectCandidates_MultipleNvidiaGpus_YieldsOncePerDeviceWithDistinctIndex()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware
            .Setup(h => h.Gpus)
            .Returns([
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 4080",
                    VramMb: 16_384,
                    MaxEncoderSessions: 12,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
                ),
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 3060",
                    VramMb: 12_288,
                    MaxEncoderSessions: 8,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
                ),
            ]);

        HardwareBenchmark sut = NewBenchmark(hardware.Object);

        List<CalibrationTarget> nvencH264 = sut.SelectCandidates()
            .Where(c => c.Encoder.FfmpegName == "h264_nvenc")
            .ToList();

        nvencH264.Should().HaveCount(2);
        nvencH264.Select(c => c.VendorIndex).Should().BeEquivalentTo([0, 1]);
        nvencH264.Select(c => c.Device!.Name).Should().BeEquivalentTo(["RTX 4080", "RTX 3060"]);
    }

    [Fact]
    public void SelectCandidates_MixedVendors_IndexesEachVendorSeparately()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware
            .Setup(h => h.Gpus)
            .Returns([
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 4080",
                    VramMb: 16_384,
                    MaxEncoderSessions: 12,
                    SupportedCodecs: [VideoCodecType.H264]
                ),
                new(
                    Vendor: GpuVendor.Intel,
                    Name: "Arc A770",
                    VramMb: 16_384,
                    MaxEncoderSessions: 8,
                    SupportedCodecs: [VideoCodecType.H264]
                ),
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 3060",
                    VramMb: 12_288,
                    MaxEncoderSessions: 8,
                    SupportedCodecs: [VideoCodecType.H264]
                ),
            ]);

        HardwareBenchmark sut = NewBenchmark(hardware.Object);

        List<CalibrationTarget> targets = sut.SelectCandidates().ToList();

        CalibrationTarget[] nvenc = targets
            .Where(t => t.Encoder.FfmpegName == "h264_nvenc")
            .ToArray();
        nvenc.Should().HaveCount(2);
        // Vendor-relative indexing: Nvidia positions inside the Nvidia-only list,
        // NOT positions inside the global Gpus list.
        nvenc.Select(t => t.VendorIndex).Should().BeEquivalentTo([0, 1]);

        CalibrationTarget[] qsv = targets.Where(t => t.Encoder.FfmpegName == "h264_qsv").ToArray();
        qsv.Should().HaveCount(1);
        qsv[0].VendorIndex.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Hardware init args — must be emitted per vendor so multi-GPU boxes
    // actually exercise each card instead of silently defaulting to GPU 0.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildCalibrationArguments_Nvidia_EmitsCudaInitWithVendorIndex()
    {
        EncoderInfo nvenc = new(
            FfmpegName: "h264_nvenc",
            RequiredVendor: GpuVendor.Nvidia,
            Presets: ["p1", "p4", "p7"],
            Profiles: ["high"],
            Levels: [],
            QualityRange: new(0, 51, 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 12,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new()
        );

        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            HardwareTarget(nvenc, "RTX 3060", vendorIndex: 1),
            1920,
            1080
        );

        int initIdx = Array.IndexOf(args, "-init_hw_device");
        initIdx.Should().BeGreaterThan(-1);
        args[initIdx + 1].Should().Be("cuda=cu:1");

        int gpuIdx = Array.IndexOf(args, "-gpu");
        gpuIdx.Should().BeGreaterThan(-1);
        args[gpuIdx + 1].Should().Be("1");
    }

    [Fact]
    public void BuildCalibrationArguments_Software_DoesNotEmitHwInit()
    {
        string[] args = HardwareBenchmark.BuildCalibrationArguments(
            SoftwareTarget(MakeSoftwareH264()),
            1920,
            1080
        );

        args.Should().NotContain("-init_hw_device");
        args.Should().NotContain("-filter_hw_device");
        args.Should().NotContain("-gpu");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TiersForTarget — 4K gating based on VRAM
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TiersForTarget_FastSoftwareTarget_IncludesUhd()
    {
        // libx264 is in the fast-software-encoder list — gets a 4K probe
        // so the speed index has a real reading instead of extrapolation.
        CalibrationTarget target = SoftwareTarget(MakeSoftwareH264());
        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target).ToArray();

        tiers.Should().Contain((3840, 2160));
        tiers.Should().Contain((1920, 1080));
    }

    [Fact]
    public void TiersForTarget_SlowSoftwareTarget_SkipsUhd()
    {
        // libaom-av1 / librav1e at 4K take 5+ minutes per probe — explicitly
        // excluded from the UHD tier so a single slow encoder doesn't block
        // the whole calibration.
        EncoderInfo libaom = MakeSoftwareH264() with
        {
            FfmpegName = "libaom-av1",
        };
        CalibrationTarget target = new(VideoCodecType.Av1, libaom, Device: null, VendorIndex: 0);
        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target).ToArray();

        tiers.Should().NotContain((3840, 2160));
        tiers.Should().Contain((1920, 1080));
    }

    [Fact]
    public void TiersForTarget_HighVramGpu_IncludesUhd()
    {
        // 16 GB GPU — UHD tier must come FIRST so heavy tier is dispatched
        // while the benchmark is still warm, not after 3 smaller ones already ran.
        EncoderInfo nvenc = new(
            FfmpegName: "h264_nvenc",
            RequiredVendor: GpuVendor.Nvidia,
            Presets: ["p4"],
            Profiles: ["high"],
            Levels: [],
            QualityRange: new(0, 51, 23),
            SupportedRateControl: [RateControlMode.Cq],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 12,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new()
        );
        CalibrationTarget target = HardwareTarget(nvenc, "RTX 4080", vendorIndex: 0);

        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target).ToArray();

        tiers[0].Should().Be((3840, 2160));
        tiers.Length.Should().Be(4);
    }

    [Fact]
    public void TiersForTarget_LowVramGpu_SkipsUhd()
    {
        // A card with 2 GB VRAM (below the 4 GB / 4000 MB cut-off) should
        // not attempt 4K — the probe risks OOM or a tiled fallback that
        // misrepresents real throughput. The threshold is 4000 MB rather
        // than 6 GB because Windows wmic reports AdapterRAM as a uint32
        // that overflows at 4095 MB on 8GB+ cards (tested on RTX 2080
        // SUPER, returns 4095 MB instead of 8192 MB). Lowering the
        // threshold keeps real 8GB+ cards on the 4K-capable side; this
        // test pins genuinely low-VRAM iGPUs to the no-4K side.
        EncoderInfo qsv = new(
            FfmpegName: "h264_qsv",
            RequiredVendor: GpuVendor.Intel,
            Presets: ["medium"],
            Profiles: ["high"],
            Levels: [],
            QualityRange: new(1, 51, 23),
            SupportedRateControl: [RateControlMode.Icq],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new()
        );
        GpuDevice lowVram = new(
            Vendor: GpuVendor.Intel,
            Name: "UHD 630",
            VramMb: 2_048,
            MaxEncoderSessions: int.MaxValue,
            SupportedCodecs: [VideoCodecType.H264]
        );
        CalibrationTarget target = new(VideoCodecType.H264, qsv, lowVram, VendorIndex: 0);

        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target).ToArray();

        tiers.Should().NotContain((3840, 2160));
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
            new()
            {
                [new(VideoCodecType.H264, "libx264", 1920, null)] = new(
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
            new()
            {
                [new(VideoCodecType.H264, "libx264", 1920, null)] = new(
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

        return new(
            new(),
            hwImpl,
            runner,
            storeImpl,
            new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
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
            QualityRange: new(0, 51, 23),
            SupportedRateControl: [RateControlMode.Crf],
            Supports10Bit: true,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "yuv420p10le",
            VendorSpecificFlags: new()
        );

    private static CalibrationTarget SoftwareTarget(EncoderInfo encoder) =>
        new(VideoCodecType.H264, encoder, Device: null, VendorIndex: 0);

    private static CalibrationTarget HardwareTarget(
        EncoderInfo encoder,
        string deviceName,
        int vendorIndex
    )
    {
        GpuDevice device = new(
            Vendor: encoder.RequiredVendor ?? GpuVendor.Nvidia,
            Name: deviceName,
            VramMb: 16_384,
            MaxEncoderSessions: 12,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
        );
        return new(VideoCodecType.H264, encoder, device, vendorIndex);
    }
}
