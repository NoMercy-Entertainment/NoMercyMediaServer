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
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.Tests.Encoder.Hardware;

public class HardwareBenchmarkTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // BuildCalibrationArguments
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildCalibrationArguments_IncludesLavfiTestSource()
    {
        EncoderInfo encoder = MakeSoftwareH264();

        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: SoftwareTarget(encoder: encoder),
            width: 1920,
            height: 1080
        );

        int fIdx = Array.IndexOf(array: args, value: "-f");
        args[fIdx + 1].Should().Be(expected: "lavfi");

        int iIdx = Array.IndexOf(array: args, value: "-i");
        args[iIdx + 1].Should().Contain(expected: "testsrc=");
        args[iIdx + 1].Should().Contain(expected: "size=1920x1080");
        args[iIdx + 1].Should().Contain(expected: "rate=30");
    }

    [Fact]
    public void BuildCalibrationArguments_UsesNullMuxerSoNothingIsWritten()
    {
        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: SoftwareTarget(encoder: MakeSoftwareH264()),
            width: 1280,
            height: 720
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
        int firstF = Array.IndexOf(array: args, value: "-f");
        int secondF = Array.IndexOf(array: args, value: "-f", startIndex: firstF + 1);
        secondF.Should().BeGreaterThan(expected: firstF);
        args[secondF + 1].Should().Be(expected: "null");
    }

    [Fact]
    public void BuildCalibrationArguments_SelectsMediumPresetForSoftware()
    {
        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: SoftwareTarget(encoder: MakeSoftwareH264()),
            width: 1280,
            height: 720
        );

        int presetIdx = Array.IndexOf(array: args, value: "-preset");
        args[presetIdx + 1].Should().Be(expected: "medium");
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
            QualityRange: new(Min: 0, Max: 51, Default: 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 12,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new()
        );

        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: HardwareTarget(encoder: nvenc, deviceName: "RTX 4080", vendorIndex: 0),
            width: 1920,
            height: 1080
        );

        int presetIdx = Array.IndexOf(array: args, value: "-preset");
        args[presetIdx + 1].Should().Be(expected: "p4");
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
            QualityRange: new(Min: 0, Max: 51, Default: 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new() { [key: "-usage"] = "transcoding" }
        );

        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: HardwareTarget(encoder: amf, deviceName: "Radeon 7900XT", vendorIndex: 0),
            width: 1280,
            height: 720
        );

        int usageIdx = Array.IndexOf(array: args, value: "-usage");
        usageIdx.Should().BeGreaterThan(expected: -1);
        args[usageIdx + 1].Should().Be(expected: "transcoding");
    }

    [Fact]
    public void BuildCalibrationArguments_IncludesProgressPipe()
    {
        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: SoftwareTarget(encoder: MakeSoftwareH264()),
            width: 1920,
            height: 1080
        );

        int idx = Array.IndexOf(array: args, value: "-progress");
        args[idx + 1].Should().Be(expected: "pipe:1");
    }

    [Fact]
    public void BuildCalibrationArguments_CapsFastEncoderFrames_AtSteadyStateLength()
    {
        // libx264 is a fast encoder — gets the long probe (300 frames over
        // ~10s of source) so the steady-state throughput is what gets
        // measured, not first-second spin-up.
        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: SoftwareTarget(encoder: MakeSoftwareH264()),
            width: 1920,
            height: 1080
        );

        int framesIdx = Array.IndexOf(array: args, value: "-frames:v");
        framesIdx.Should().BeGreaterThan(expected: -1);
        int frameCount = int.Parse(s: args[framesIdx + 1]);
        frameCount.Should().Be(expected: 300);
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
        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: new(Codec: VideoCodecType.Av1, Encoder: libaom, Device: null, VendorIndex: 0),
            width: 1920,
            height: 1080
        );

        int framesIdx = Array.IndexOf(array: args, value: "-frames:v");
        framesIdx.Should().BeGreaterThan(expected: -1);
        int frameCount = int.Parse(s: args[framesIdx + 1]);
        frameCount.Should().Be(expected: 60);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SelectCandidates
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SelectCandidates_ExcludesHwEncodersForMissingVendor()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(expression: h => h.Gpus).Returns(value: []); // no GPUs installed

        HardwareBenchmark sut = NewBenchmark(hardware: hardware.Object);

        List<EncoderInfo> candidates = sut.SelectCandidates().Select(selector: c => c.Encoder).ToList();

        // Software encoders (no RequiredVendor) must remain.
        candidates.Should().Contain(predicate: e => e.FfmpegName == "libx264");
        // Hardware encoders must be filtered out.
        candidates.Should().NotContain(predicate: e => e.FfmpegName == "h264_nvenc");
        candidates.Should().NotContain(predicate: e => e.FfmpegName == "h264_amf");
        candidates.Should().NotContain(predicate: e => e.FfmpegName == "h264_qsv");
    }

    [Fact]
    public void SelectCandidates_IncludesHwEncodersForPresentVendor()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware
            .Setup(expression: h => h.Gpus)
            .Returns(value:
            [
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 4080",
                    VramMb: 16_384,
                    MaxEncoderSessions: 12,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
                ),
            ]);

        HardwareBenchmark sut = NewBenchmark(hardware: hardware.Object);

        List<string> names = sut.SelectCandidates().Select(selector: c => c.Encoder.FfmpegName).ToList();

        names.Should().Contain(expected: "h264_nvenc");
        names.Should().NotContain(unexpected: "h264_amf");
    }

    [Fact]
    public void SelectCandidates_MultipleNvidiaGpus_YieldsOncePerDeviceWithDistinctIndex()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware
            .Setup(expression: h => h.Gpus)
            .Returns(value:
            [
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

        HardwareBenchmark sut = NewBenchmark(hardware: hardware.Object);

        List<CalibrationTarget> nvencH264 = sut.SelectCandidates()
            .Where(predicate: c => c.Encoder.FfmpegName == "h264_nvenc")
            .ToList();

        nvencH264.Should().HaveCount(expected: 2);
        nvencH264.Select(selector: c => c.VendorIndex).Should().BeEquivalentTo(expectation: [0, 1]);
        nvencH264.Select(selector: c => c.Device!.Name).Should().BeEquivalentTo(expectation: ["RTX 4080", "RTX 3060"]);
    }

    [Fact]
    public void SelectCandidates_MixedVendors_IndexesEachVendorSeparately()
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware
            .Setup(expression: h => h.Gpus)
            .Returns(value:
            [
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

        HardwareBenchmark sut = NewBenchmark(hardware: hardware.Object);

        List<CalibrationTarget> targets = sut.SelectCandidates().ToList();

        CalibrationTarget[] nvenc = targets
            .Where(predicate: t => t.Encoder.FfmpegName == "h264_nvenc")
            .ToArray();
        nvenc.Should().HaveCount(expected: 2);
        // Vendor-relative indexing: Nvidia positions inside the Nvidia-only list,
        // NOT positions inside the global Gpus list.
        nvenc.Select(selector: t => t.VendorIndex).Should().BeEquivalentTo(expectation: [0, 1]);

        CalibrationTarget[] qsv = targets.Where(predicate: t => t.Encoder.FfmpegName == "h264_qsv").ToArray();
        qsv.Should().HaveCount(expected: 1);
        qsv[0].VendorIndex.Should().Be(expected: 0);
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
            QualityRange: new(Min: 0, Max: 51, Default: 23),
            SupportedRateControl: [RateControlMode.Cqp],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 12,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new()
        );

        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: HardwareTarget(encoder: nvenc, deviceName: "RTX 3060", vendorIndex: 1),
            width: 1920,
            height: 1080
        );

        int initIdx = Array.IndexOf(array: args, value: "-init_hw_device");
        initIdx.Should().BeGreaterThan(expected: -1);
        args[initIdx + 1].Should().Be(expected: "cuda=cu:1");

        int gpuIdx = Array.IndexOf(array: args, value: "-gpu");
        gpuIdx.Should().BeGreaterThan(expected: -1);
        args[gpuIdx + 1].Should().Be(expected: "1");
    }

    [Fact]
    public void BuildCalibrationArguments_Software_DoesNotEmitHwInit()
    {
        string[] args = CalibrationArgumentBuilder.BuildCalibrationArguments(
            target: SoftwareTarget(encoder: MakeSoftwareH264()),
            width: 1920,
            height: 1080
        );

        args.Should().NotContain(unexpected: "-init_hw_device");
        args.Should().NotContain(unexpected: "-filter_hw_device");
        args.Should().NotContain(unexpected: "-gpu");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TiersForTarget — 4K gating based on VRAM
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TiersForTarget_FastSoftwareTarget_IncludesUhd()
    {
        // libx264 is in the fast-software-encoder list — gets a 4K probe
        // so the speed index has a real reading instead of extrapolation.
        CalibrationTarget target = SoftwareTarget(encoder: MakeSoftwareH264());
        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target: target).ToArray();

        tiers.Should().Contain(expected: (3840, 2160));
        tiers.Should().Contain(expected: (1920, 1080));
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
        CalibrationTarget target = new(Codec: VideoCodecType.Av1, Encoder: libaom, Device: null, VendorIndex: 0);
        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target: target).ToArray();

        tiers.Should().NotContain(unexpected: (3840, 2160));
        tiers.Should().Contain(expected: (1920, 1080));
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
            QualityRange: new(Min: 0, Max: 51, Default: 23),
            SupportedRateControl: [RateControlMode.Cq],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: 12,
            PixelFormat10Bit: "",
            VendorSpecificFlags: new()
        );
        CalibrationTarget target = HardwareTarget(encoder: nvenc, deviceName: "RTX 4080", vendorIndex: 0);

        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target: target).ToArray();

        tiers[0].Should().Be(expected: (3840, 2160));
        tiers.Length.Should().Be(expected: 4);
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
            QualityRange: new(Min: 1, Max: 51, Default: 23),
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
        CalibrationTarget target = new(Codec: VideoCodecType.H264, Encoder: qsv, Device: lowVram, VendorIndex: 0);

        (int W, int H)[] tiers = HardwareBenchmark.TiersForTarget(target: target).ToArray();

        tiers.Should().NotContain(unexpected: (3840, 2160));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalibrateAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalibrateAsync_PopulatesIndexFromProgressOutput()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
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
                valueFunction: (
                    string _,
                    string[] _,
                    Action<string>? onStdOut,
                    Action<string>? _,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    // Feed a typical ffmpeg -progress payload: 60 fps, end marker.
                    onStdOut?.Invoke(obj: "frame=150");
                    onStdOut?.Invoke(obj: "fps=60");
                    onStdOut?.Invoke(obj: "speed=2.0x");
                    onStdOut?.Invoke(obj: "progress=end");
                    return Task.FromResult(
                        result: new ProcessResult(
                            ExitCode: 0,
                            StdOut: "",
                            StdErr: "",
                            Duration: TimeSpan.Zero
                        )
                    );
                }
            );

        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(expression: h => h.Gpus).Returns(value: []);

        Mock<ISpeedIndexStore> store = new();

        HardwareBenchmark sut = NewBenchmark(hardware: hardware.Object, processRunner: runner.Object, store: store.Object);

        SpeedIndex index = await sut.CalibrateAsync(ct: CancellationToken.None);

        index.Measurements.Should().NotBeEmpty();
        index.Measurements.Values.Should().AllSatisfy(expected: m => m.Fps.Should().Be(expected: 60));
        index.Measurements.Values.Should().AllSatisfy(expected: m => m.SpeedMultiplier.Should().Be(expected: 2.0));
        store.Verify(expression: s => s.Save(It.IsAny<SpeedIndex>()), times: Times.Once);
    }

    [Fact]
    public async Task CalibrateAsync_SkipsFailedEncoderRuns()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
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
                value: Task.FromResult(
                    result: new ProcessResult(
                        ExitCode: 1,
                        StdOut: "",
                        StdErr: "boom",
                        Duration: TimeSpan.Zero
                    )
                )
            );

        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(expression: h => h.Gpus).Returns(value: []);
        Mock<ISpeedIndexStore> store = new();

        HardwareBenchmark sut = NewBenchmark(hardware: hardware.Object, processRunner: runner.Object, store: store.Object);

        SpeedIndex index = await sut.CalibrateAsync(ct: CancellationToken.None);

        index.Measurements.Should().BeEmpty();
    }

    [Fact]
    public void NeedsRecalibration_EmptyCache_ReturnsTrue()
    {
        Mock<ISpeedIndexStore> store = new();
        store.Setup(expression: s => s.Load()).Returns(value: (SpeedIndex?)null);
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: (DateTime?)null);

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeTrue();
    }

    [Fact]
    public void NeedsRecalibration_OldCache_ReturnsTrue()
    {
        Mock<ISpeedIndexStore> store = new();
        SpeedIndex index = new(
            Measurements: new()
            {
                [key: new(Codec: VideoCodecType.H264, Encoder: "libx264", Width: 1920, DeviceName: null)] = new(
                    Fps: 60,
                    SpeedMultiplier: 2.0,
                    MeasuredAt: DateTime.UtcNow.AddDays(value: -60)
                ),
            }
        );
        store.Setup(expression: s => s.Load()).Returns(value: index);
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -60));

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeTrue();
    }

    [Fact]
    public void NeedsRecalibration_FreshCache_ReturnsFalse()
    {
        Mock<ISpeedIndexStore> store = new();
        SpeedIndex index = new(
            Measurements: new()
            {
                [key: new(Codec: VideoCodecType.H264, Encoder: "libx264", Width: 1920, DeviceName: null)] = new(
                    Fps: 60,
                    SpeedMultiplier: 2.0,
                    MeasuredAt: DateTime.UtcNow.AddDays(value: -1)
                ),
            }
        );
        store.Setup(expression: s => s.Load()).Returns(value: index);
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddDays(value: -1));
        // Fresh cache means the schema version also matches the current
        // build's BenchmarkSchemaVersion — without this the version-mismatch
        // branch fires and forces recalibration regardless of calendar age.
        store
            .Setup(expression: s => s.LoadedSchemaVersion)
            .Returns(value: HardwareBenchmark.BenchmarkSchemaVersion);

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeFalse();
    }

    [Fact]
    public void NeedsRecalibration_OlderSchemaVersion_ReturnsTrue()
    {
        // A cache file written by an older benchmark version measured a
        // different probe shape — the numbers are not comparable to what
        // current code would emit and must be discarded even when the
        // calendar grace window has not elapsed.
        Mock<ISpeedIndexStore> store = new();
        SpeedIndex index = new(
            Measurements: new()
            {
                [key: new(Codec: VideoCodecType.H264, Encoder: "libx264", Width: 1920, DeviceName: null)] = new(
                    Fps: 60,
                    SpeedMultiplier: 2.0,
                    MeasuredAt: DateTime.UtcNow.AddHours(value: -1)
                ),
            }
        );
        store.Setup(expression: s => s.Load()).Returns(value: index);
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddHours(value: -1));
        store.Setup(expression: s => s.LoadedSchemaVersion).Returns(value: 0);

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeTrue();
    }

    [Fact]
    public void NeedsRecalibration_NullSchemaVersion_ReturnsTrue()
    {
        // Cache files written before the SchemaVersion field existed load
        // with LoadedSchemaVersion = null. Treat null as "older than every
        // shipped version" so we recalibrate instead of trusting unknown
        // numbers.
        Mock<ISpeedIndexStore> store = new();
        SpeedIndex index = new(
            Measurements: new()
            {
                [key: new(Codec: VideoCodecType.H264, Encoder: "libx264", Width: 1920, DeviceName: null)] = new(
                    Fps: 60,
                    SpeedMultiplier: 2.0,
                    MeasuredAt: DateTime.UtcNow.AddHours(value: -1)
                ),
            }
        );
        store.Setup(expression: s => s.Load()).Returns(value: index);
        store.Setup(expression: s => s.LastCalibratedAt).Returns(value: DateTime.UtcNow.AddHours(value: -1));
        store.Setup(expression: s => s.LoadedSchemaVersion).Returns(value: (int?)null);

        HardwareBenchmark sut = NewBenchmark(store: store.Object);

        sut.NeedsRecalibration().Should().BeTrue();
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
        hw.Setup(expression: h => h.Gpus).Returns(value: []);
        IHardwareCapabilities hwImpl = hardware ?? hw.Object;

        IProcessRunner runner =
            processRunner ?? new Mock<IProcessRunner>(behavior: MockBehavior.Loose).Object;
        ISpeedIndexStore storeImpl = store ?? new Mock<ISpeedIndexStore>().Object;

        return new(
            codecRegistry: new(),
            hardware: hwImpl,
            processRunner: runner,
            store: storeImpl,
            options: new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            logger: NullLogger<HardwareBenchmark>.Instance
        );
    }

    private static EncoderInfo MakeSoftwareH264() =>
        new(
            FfmpegName: "libx264",
            RequiredVendor: null,
            Presets: ["ultrafast", "fast", "medium", "slow", "veryslow"],
            Profiles: ["high"],
            Levels: ["4.1"],
            QualityRange: new(Min: 0, Max: 51, Default: 23),
            SupportedRateControl: [RateControlMode.Crf],
            Supports10Bit: true,
            SupportsHdr: false,
            MaxConcurrentSessions: int.MaxValue,
            PixelFormat10Bit: "yuv420p10le",
            VendorSpecificFlags: new()
        );

    private static CalibrationTarget SoftwareTarget(EncoderInfo encoder) =>
        new(Codec: VideoCodecType.H264, Encoder: encoder, Device: null, VendorIndex: 0);

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
        return new(Codec: VideoCodecType.H264, Encoder: encoder, Device: device, VendorIndex: vendorIndex);
    }
}
