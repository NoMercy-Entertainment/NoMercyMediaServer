using NoMercy.Encoder.Output;
using NoMercy.Encoder.Strategies.Shared;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Strategies.Shared;

/// <summary>
/// TaskResourceHelper decides whether a decomposed task needs a GPU slot or
/// just CPU threads. Wrong classification means a CPU-only encode reserves
/// GPU slots it never uses (stall), or a GPU encode runs without a slot
/// reservation (oversubscribes the engine).
/// </summary>
public class TaskResourceHelperTests
{
    private static VideoOutputPlan VideoWith(string encoderName) =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: encoderName,
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "main",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v]",
            ExtraFlags: []
        );

    // ── GPU encoder detection ──────────────────────────────────────────────

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    [InlineData("h264_amf")]
    [InlineData("hevc_amf")]
    [InlineData("h264_qsv")]
    [InlineData("hevc_qsv")]
    [InlineData("av1_qsv")]
    [InlineData("vp9_qsv")]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    [InlineData("av1_vaapi")]
    [InlineData("h264_videotoolbox")]
    [InlineData("hevc_videotoolbox")]
    [InlineData("h264_cuvid")] // decoder, but treated as GPU too
    public void ForVideoOutput_GpuEncoder_ReservesGpuSlot(string encoderName)
    {
        VideoOutputPlan plan = VideoWith(encoderName);
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan);

        req.GpuSlots.Should().Be(1);
        req.GpuDeviceKey.Should().Be(encoderName);
        req.CpuThreads.Should().Be(2); // GPU encodes still spawn 2 CPU helper threads
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    [InlineData("libaom-av1")]
    [InlineData("libvpx-vp9")]
    public void ForVideoOutput_SoftwareEncoder_ReservesCpuOnly(string encoderName)
    {
        VideoOutputPlan plan = VideoWith(encoderName);
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan);

        req.GpuSlots.Should().Be(0);
        req.GpuDeviceKey.Should().BeNull();
        req.CpuThreads.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ForVideoOutput_SoftwareEncoder_UsesHalfTheCpuCores()
    {
        VideoOutputPlan plan = VideoWith("libx264");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan);

        int expected = Math.Max(1, Environment.ProcessorCount / 2);
        req.CpuThreads.Should().Be(expected);
    }

    [Fact]
    public void ForVideoOutput_EmptyEncoder_TreatedAsSoftware()
    {
        VideoOutputPlan plan = VideoWith("");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan);

        req.GpuSlots.Should().Be(0);
        req.GpuDeviceKey.Should().BeNull();
    }

    [Fact]
    public void ForVideoOutput_UnknownEncoder_TreatedAsSoftware()
    {
        VideoOutputPlan plan = VideoWith("totally_made_up_encoder");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan);

        req.GpuSlots.Should().Be(0);
    }

    [Fact]
    public void ForVideoOutput_CaseInsensitive_StillDetectsGpu()
    {
        // Encoder names from upstream sources may differ in case.
        VideoOutputPlan plan = VideoWith("H264_NVENC");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan);

        req.GpuSlots.Should().Be(1);
    }

    // ── CpuOnly helper ──────────────────────────────────────────────────────

    [Fact]
    public void CpuOnly_DefaultsToOneThread()
    {
        ResourceRequirement req = TaskResourceHelper.CpuOnly();

        req.GpuSlots.Should().Be(0);
        req.GpuDeviceKey.Should().BeNull();
        req.CpuThreads.Should().Be(1);
    }

    [Fact]
    public void CpuOnly_CustomThreadCount_FlowsThrough()
    {
        ResourceRequirement req = TaskResourceHelper.CpuOnly(cpuThreads: 8);

        req.CpuThreads.Should().Be(8);
    }
}
