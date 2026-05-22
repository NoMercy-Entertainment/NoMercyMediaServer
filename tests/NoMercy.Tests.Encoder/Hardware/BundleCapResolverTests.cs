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
        new HardwareCapabilities(gpu is null ? [] : [gpu], cpuCores);

    private static IHardwareBenchmark MakeBenchmark(SpeedIndex? index)
    {
        Mock<IHardwareBenchmark> mock = new();
        mock.Setup(b => b.GetCachedIndex()).Returns(index!);
        return mock.Object;
    }

    private static SpeedIndex IndexWith(params (SpeedKey key, double speed)[] entries)
    {
        Dictionary<SpeedKey, SpeedMeasurement> dict = entries.ToDictionary(
            t => t.key,
            t => new SpeedMeasurement(t.speed * 30, t.speed, DateTime.UtcNow)
        );
        return new SpeedIndex(dict);
    }

    // ── Fallbacks when no benchmark or hardware available ───────────────────

    [Fact]
    public void Resolve_NoBenchmark_UsesConservativeFallback()
    {
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];
        IHardwareCapabilities hw = MakeHardware(MakeGpu());

        (int gpuCap, int cpuCap) = BundleCapResolver.Resolve(rungs, benchmark: null, hardware: hw);

        gpuCap.Should().Be(2); // UnknownGpuCapFallback
        cpuCap.Should().Be(1); // UnknownCpuCapFallback
    }

    [Fact]
    public void Resolve_NoHardware_GpuCapFalls()
    {
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];
        IHardwareBenchmark benchmark = MakeBenchmark(null);

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, benchmark, hardware: null);

        gpuCap.Should().Be(2);
    }

    [Fact]
    public void Resolve_NoMatchingMeasurement_UsesConservativeFallback()
    {
        // Benchmark has data for a different codec/width than what's in the plan.
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H264, "h264_nvenc", 1280, "RTX 4080"), 10.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        gpuCap.Should().Be(2);
    }

    // ── Benchmark-driven caps ───────────────────────────────────────────────

    [Fact]
    public void Resolve_FastGpu_AllowsLargeBundle()
    {
        // 12× realtime / 1.5× target = 8 streams per bundle.
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1920, "RTX 4080"), 12.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        gpuCap.Should().Be(8);
    }

    [Fact]
    public void Resolve_SlowGpu_StillReturnsAtLeastOne()
    {
        // 1× realtime / 1.5× target = 0 → floor at 1.
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1920, "RTX 4080"), 1.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        gpuCap.Should().Be(1);
    }

    [Fact]
    public void Resolve_MultipleRungs_PickedByTheSlowest()
    {
        // 4K HEVC (slow) + 1080p HEVC (fast) — slowest sets the cap.
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 3840, "RTX 4080"), 3.0),
            (new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1920, "RTX 4080"), 12.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu());
        BundleCapResolver.PlannedRung[] rungs =
        [
            Rung("hevc_nvenc", 3840, isGpu: true),
            Rung("hevc_nvenc", 1920, isGpu: true),
        ];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        gpuCap.Should().Be(2); // floor(3.0 / 1.5) = 2
    }

    [Fact]
    public void Resolve_DriverCapAppliesAsCeiling()
    {
        // Benchmark says 12 / 1.5 = 8 but driver caps at 5 → 5.
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1920, "RTX 4080"), 12.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu(maxSessions: 5));
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        gpuCap.Should().Be(5);
    }

    [Fact]
    public void Resolve_UnlimitedDriverCap_DoesNotConstrain()
    {
        // Professional/datacenter cards report MaxEncoderSessions = int.MaxValue.
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1920, "L40"), 30.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu("L40", maxSessions: int.MaxValue));
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        gpuCap.Should().Be(20); // 30/1.5 = 20, no driver clamp
    }

    [Fact]
    public void Resolve_CpuRungsScoredSeparately()
    {
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H265, "libx265", 1920, null), 6.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung("libx265", 1920, isGpu: false)];

        (_, int cpuCap) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        cpuCap.Should().Be(4); // floor(6/1.5)
    }

    [Fact]
    public void Resolve_GpuRungsDontAffectCpuCap()
    {
        // CPU plan has nothing — should hit the CPU fallback (1) regardless
        // of how good the GPU benchmark looks.
        SpeedIndex index = IndexWith(
            (new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1920, "RTX 4080"), 12.0)
        );
        IHardwareBenchmark benchmark = MakeBenchmark(index);
        IHardwareCapabilities hw = MakeHardware(MakeGpu());
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, int cpuCap) = BundleCapResolver.Resolve(rungs, benchmark, hw);

        gpuCap.Should().Be(8);
        cpuCap.Should().Be(1);
    }

    private static BundleCapResolver.PlannedRung Rung(string encoder, int width, bool isGpu)
    {
        VideoCodecType codec =
            encoder.Contains("hevc") || encoder.Contains("x265") ? VideoCodecType.H265
            : encoder.Contains("av1") ? VideoCodecType.Av1
            : encoder.Contains("vp9") ? VideoCodecType.Vp9
            : VideoCodecType.H264;
        return new(Codec: codec, EncoderName: encoder, Width: width, IsGpu: isGpu);
    }
}
