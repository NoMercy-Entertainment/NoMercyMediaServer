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

using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// Bundle-cap resolution from real benchmark measurements. Caps are the
/// practical-throughput ceiling, not the driver-allowed maximum, so a weak
/// GPU earns a smaller cap and a strong GPU earns a larger one — based on
/// what fps each rung actually hit on this exact host.
/// </summary>
public class BundleCapResolverTests
{
    private static GpuDevice MakeGpu(string name = "RTX 4080", int maxSessions = 8) =>
        new(
            Vendor: GpuVendor.Nvidia,
            Name: name,
            VramMb: 16384,
            MaxEncoderSessions: maxSessions,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );

    private static IHardwareCapabilities MakeHardware(GpuDevice? gpu, int cpuCores = 16) =>
        new HardwareCapabilities(Gpus: gpu is null ? [] : [gpu], CpuCores: cpuCores);

    private static IHardwareBenchmark MakeBenchmark(SpeedIndex? index)
    {
        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(expression: b => b.GetCachedIndex()).Returns(value: index!);
        return mock.Object;
    }

    private static SpeedIndex IndexWith(params (SpeedKey key, double speed)[] entries)
    {
        Dictionary<SpeedKey, SpeedMeasurement> dict = entries.ToDictionary(
            keySelector: t => t.key,
            elementSelector: t => new SpeedMeasurement(Fps: t.speed * 30, SpeedMultiplier: t.speed, MeasuredAt: DateTime.UtcNow)
        );
        return new(Measurements: dict);
    }

    // ── Fallbacks when no benchmark or hardware available ───────────────────

    [Fact]
    public void Resolve_NoBenchmark_UsesConservativeFallback()
    {
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu());

        (int gpuCap, int cpuCap) = BundleCapResolver.Resolve(rungs: rungs, benchmark: null, hardware: hw);

        gpuCap.Should().Be(expected: 2); // UnknownGpuCapFallback
        cpuCap.Should().Be(expected: 1); // UnknownCpuCapFallback
    }

    [Fact]
    public void Resolve_NoHardware_GpuCapFalls()
    {
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];
        IHardwareBenchmark benchmark = MakeBenchmark(index: null);

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: null);

        gpuCap.Should().Be(expected: 2);
    }

    [Fact]
    public void Resolve_NoMatchingMeasurement_UsesConservativeFallback()
    {
        // Benchmark has data for a different codec/width than what's in the plan.
        SpeedIndex index = IndexWith(
            entries: (new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1280, DeviceName: "RTX 4080"), 10.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        gpuCap.Should().Be(expected: 2);
    }

    // ── Benchmark-driven caps ───────────────────────────────────────────────

    [Fact]
    public void Resolve_FastGpu_AllowsLargeBundle()
    {
        // 12× realtime / 1.5× target = 8 streams per bundle.
        SpeedIndex index = IndexWith(
            entries: (new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: "RTX 4080"), 12.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        gpuCap.Should().Be(expected: 8);
    }

    [Fact]
    public void Resolve_SlowGpu_StillReturnsAtLeastOne()
    {
        // 1× realtime / 1.5× target = 0 → floor at 1.
        SpeedIndex index = IndexWith(
            entries: (new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: "RTX 4080"), 1.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        gpuCap.Should().Be(expected: 1);
    }

    [Fact]
    public void Resolve_MultipleRungs_PickedByTheSlowest()
    {
        // 4K HEVC (slow) + 1080p HEVC (fast) — slowest sets the cap.
        SpeedIndex index = IndexWith(entries: [(new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 3840, DeviceName: "RTX 4080"), 3.0), (new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: "RTX 4080"), 12.0)]
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu());
        BundleCapResolver.PlannedRung[] rungs =
        [
            Rung(encoder: "hevc_nvenc", width: 3840, isGpu: true),
            Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true),
        ];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        gpuCap.Should().Be(expected: 2); // floor(3.0 / 1.5) = 2
    }

    [Fact]
    public void Resolve_DriverCapAppliesAsCeiling()
    {
        // Benchmark says 12 / 1.5 = 8 but driver caps at 5 → 5.
        SpeedIndex index = IndexWith(
            entries: (new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: "RTX 4080"), 12.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu(maxSessions: 5));
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        gpuCap.Should().Be(expected: 5);
    }

    [Fact]
    public void Resolve_UnlimitedDriverCap_DoesNotConstrain()
    {
        // Professional/datacenter cards report MaxEncoderSessions = int.MaxValue.
        SpeedIndex index = IndexWith(
            entries: (new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: "L40"), 30.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu(name: "L40", maxSessions: int.MaxValue));
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        gpuCap.Should().Be(expected: 20); // 30/1.5 = 20, no driver clamp
    }

    [Fact]
    public void Resolve_CpuRungsScoredSeparately()
    {
        SpeedIndex index = IndexWith(
            entries: (new(Codec: VideoCodecType.H265, Encoder: "libx265", Width: 1920, DeviceName: null), 6.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "libx265", width: 1920, isGpu: false)];

        (_, int cpuCap) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        cpuCap.Should().Be(expected: 4); // floor(6/1.5)
    }

    [Fact]
    public void Resolve_GpuRungsDontAffectCpuCap()
    {
        // CPU plan has nothing — should hit the CPU fallback (1) regardless
        // of how good the GPU benchmark looks.
        SpeedIndex index = IndexWith(
            entries: (new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: "RTX 4080"), 12.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index: index);
        IHardwareCapabilities hw = MakeHardware(gpu: MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung(encoder: "hevc_nvenc", width: 1920, isGpu: true)];

        (int gpuCap, int cpuCap) = BundleCapResolver.Resolve(rungs: rungs, benchmark: benchmark, hardware: hw);

        gpuCap.Should().Be(expected: 8);
        cpuCap.Should().Be(expected: 1);
    }

    private static BundleCapResolver.PlannedRung Rung(string encoder, int width, bool isGpu)
    {
        VideoCodecType codec =
            encoder.Contains(value: "hevc") || encoder.Contains(value: "x265") ? VideoCodecType.H265
            : encoder.Contains(value: "av1") ? VideoCodecType.Av1
            : encoder.Contains(value: "vp9") ? VideoCodecType.Vp9
            : VideoCodecType.H264;
        return new(Codec: codec, EncoderName: encoder, Width: width, IsGpu: isGpu);
    }
}
