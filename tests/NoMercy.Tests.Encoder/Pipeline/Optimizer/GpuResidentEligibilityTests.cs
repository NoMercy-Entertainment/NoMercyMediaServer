using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class GpuResidentEligibilityTests
{
    [Fact]
    public void Eligible_PureDecodeScaleEncode_True()
    {
        bool eligible = GpuResidentEligibility.IsEligible([Video()], []);
        eligible.Should().BeTrue("decode→scale→encode keeps frames on the GPU");
    }

    [Fact]
    public void Ineligible_HdrTonemap_False()
    {
        VideoOutputPlan hdr = Video() with { ConvertHdrToSdr = true };
        GpuResidentEligibility.IsEligible([hdr], []).Should().BeFalse("CPU tonemap in the graph");
    }

    [Fact]
    public void Ineligible_Crop_False()
    {
        VideoOutputPlan cropped = Video() with { CropFilter = "1920:800:0:140" };
        GpuResidentEligibility.IsEligible([cropped], []).Should().BeFalse();
    }

    [Fact]
    public void Ineligible_SubtitleBurnIn_False()
    {
        SubtitleOutputPlan burnIn = new(
            OutputCodec: SubtitleCodecType.Ass,
            Action: StreamAction.Extract,
            Language: "en",
            SourceIndex: 0,
            MapLabel: null,
            Policy: SubtitlePolicy.BurnIn
        );
        GpuResidentEligibility.IsEligible([Video()], [burnIn]).Should().BeFalse();
    }

    [Fact]
    public void Ineligible_NoVideo_False()
    {
        GpuResidentEligibility.IsEligible([], []).Should().BeFalse();
    }

    private static VideoOutputPlan Video() =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "h264_nvenc",
            Crf: 23,
            BitrateKbps: 5000,
            Preset: "p4",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: new()
        );
}
