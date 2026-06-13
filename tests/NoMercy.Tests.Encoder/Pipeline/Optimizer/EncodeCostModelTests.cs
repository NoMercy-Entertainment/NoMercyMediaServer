using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline.Optimizer;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class EncodeCostModelTests
{
    private static readonly SpeedIndex EmptySpeed = new([]);

    [Fact]
    public void RungCost_HigherResolution_CostsMore()
    {
        EncodeCostModel model = new(EmptySpeed);

        double uhd = model.RungCost(3840, 2160, VideoCodecType.H265, "libx265", passes: 1);
        double fhd = model.RungCost(1920, 1080, VideoCodecType.H265, "libx265", passes: 1);

        uhd.Should().BeGreaterThan(fhd, "more pixels = more encode work");
    }

    [Fact]
    public void RungCost_TwoPass_DoublesSinglePass()
    {
        EncodeCostModel model = new(EmptySpeed);

        double single = model.RungCost(1920, 1080, VideoCodecType.H264, "libx264", passes: 1);
        double twoPass = model.RungCost(1920, 1080, VideoCodecType.H264, "libx264", passes: 2);

        twoPass.Should().BeApproximately(single * 2, 0.001);
    }

    [Fact]
    public void RungCost_FasterEncoder_CostsLess()
    {
        // A benchmarked encoder with 4x realtime is cheaper than the 1.0 default.
        SpeedIndex fast = new(
            new()
            {
                [new(VideoCodecType.H265, "hevc_nvenc", 1920, null)] = new(
                    Fps: 120,
                    SpeedMultiplier: 4.0,
                    MeasuredAt: default
                ),
            }
        );
        EncodeCostModel hw = new(fast);
        EncodeCostModel sw = new(EmptySpeed);

        double hwCost = hw.RungCost(1920, 1080, VideoCodecType.H265, "hevc_nvenc", passes: 1);
        double swCost = sw.RungCost(1920, 1080, VideoCodecType.H265, "libx265", passes: 1);

        hwCost.Should().BeLessThan(swCost, "a 4x-realtime encoder costs less wall-time");
    }

    [Fact]
    public void TotalCost_SumsRungsPlusDecodeAndTonemap()
    {
        EncodeCostModel model = new(EmptySpeed);

        double withTonemap = model.TotalCost(
            sourceWidth: 3840,
            sourceHeight: 2160,
            sourceIsHdr: true,
            rungs:
            [
                new(1920, 1080, VideoCodecType.H265, "libx265", 1),
                new(1280, 720, VideoCodecType.H265, "libx265", 1),
            ]
        );
        double withoutTonemap = model.TotalCost(
            sourceWidth: 3840,
            sourceHeight: 2160,
            sourceIsHdr: false,
            rungs:
            [
                new(1920, 1080, VideoCodecType.H265, "libx265", 1),
                new(1280, 720, VideoCodecType.H265, "libx265", 1),
            ]
        );

        withTonemap.Should().BeGreaterThan(withoutTonemap, "HDR adds a tonemap pass");
    }
}
