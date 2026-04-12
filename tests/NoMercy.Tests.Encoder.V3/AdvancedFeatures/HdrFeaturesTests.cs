namespace NoMercy.Tests.Encoder.V3.AdvancedFeatures;

using Moq;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Hdr;

public class HdrFeaturesTests
{
    private readonly Mock<IHardwareCapabilities> _hardwareMock = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegMock = new();

    [Fact]
    public void TonemapSelector_SelectsLibplacebo_WhenFilterAvailable()
    {
        _ffmpegMock.Setup(f => f.HasFilter("libplacebo")).Returns(true);
        _ffmpegMock.Setup(f => f.HasFilter("tonemap_opencl")).Returns(false);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(_hardwareMock.Object, _ffmpegMock.Object);

        strategy.Method.Should().Be(TonemapMethod.Libplacebo);
        strategy.IsGpuAccelerated.Should().BeTrue();
        strategy.FfmpegFilterChain.Should().Contain("libplacebo");
    }

    [Fact]
    public void TonemapSelector_SelectsTonemapOpencl_WhenLibplaceboNotAvailable()
    {
        _ffmpegMock.Setup(f => f.HasFilter("libplacebo")).Returns(false);
        _ffmpegMock.Setup(f => f.HasFilter("tonemap_opencl")).Returns(true);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(_hardwareMock.Object, _ffmpegMock.Object);

        strategy.Method.Should().Be(TonemapMethod.TonemapOpencl);
        strategy.IsGpuAccelerated.Should().BeTrue();
        strategy.FfmpegFilterChain.Should().Contain("tonemap_opencl");
    }

    [Fact]
    public void TonemapSelector_FallsBackToZscale_WhenNoGpuFiltersAvailable()
    {
        _ffmpegMock.Setup(f => f.HasFilter("libplacebo")).Returns(false);
        _ffmpegMock.Setup(f => f.HasFilter("tonemap_opencl")).Returns(false);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(_hardwareMock.Object, _ffmpegMock.Object);

        strategy.Method.Should().Be(TonemapMethod.ZscaleTonemap);
        strategy.IsGpuAccelerated.Should().BeFalse();
        strategy.FfmpegFilterChain.Should().Contain("zscale");
    }

    [Fact]
    public void TonemapSelector_LibplaceboTakesPriorityOverOpencl()
    {
        _ffmpegMock.Setup(f => f.HasFilter("libplacebo")).Returns(true);
        _ffmpegMock.Setup(f => f.HasFilter("tonemap_opencl")).Returns(true);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(_hardwareMock.Object, _ffmpegMock.Object);

        strategy.Method.Should().Be(TonemapMethod.Libplacebo);
    }

    [Fact]
    public void TonemapStrategy_ConstructsCorrectly()
    {
        TonemapStrategy strategy = new(
            Method: TonemapMethod.CustomLut,
            FfmpegFilterChain: "lut3d=file=custom.cube",
            IsGpuAccelerated: false
        );

        strategy.Method.Should().Be(TonemapMethod.CustomLut);
        strategy.FfmpegFilterChain.Should().Be("lut3d=file=custom.cube");
        strategy.IsGpuAccelerated.Should().BeFalse();
    }

    [Fact]
    public void TonemapMethod_HasExpectedValues()
    {
        TonemapMethod[] values = Enum.GetValues<TonemapMethod>();

        values.Should().Contain(TonemapMethod.Libplacebo);
        values.Should().Contain(TonemapMethod.TonemapOpencl);
        values.Should().Contain(TonemapMethod.ZscaleTonemap);
        values.Should().Contain(TonemapMethod.CustomLut);
        values.Should().HaveCount(4);
    }

    [Fact]
    public void HdrOptions_DefaultValues_AreCorrect()
    {
        HdrOptions options = new();

        options.Algorithm.Should().Be(TonemapAlgorithm.Hable);
        options.CustomLutPath.Should().BeNull();
        options.LutApply.Should().Be(LutApplication.AfterTonemap);
        options.Desat.Should().Be(0.0);
        options.Peak.Should().Be(0.0);
        options.PreserveMetadata.Should().BeFalse();
    }

    [Fact]
    public void HdrOptions_ConstructsWithCustomValues()
    {
        HdrOptions options = new(
            Algorithm: TonemapAlgorithm.Reinhard,
            CustomLutPath: "/luts/custom.cube",
            LutApply: LutApplication.BeforeTonemap,
            Desat: 0.5,
            Peak: 1000.0,
            PreserveMetadata: true
        );

        options.Algorithm.Should().Be(TonemapAlgorithm.Reinhard);
        options.CustomLutPath.Should().Be("/luts/custom.cube");
        options.LutApply.Should().Be(LutApplication.BeforeTonemap);
        options.Desat.Should().BeApproximately(0.5, 0.001);
        options.Peak.Should().BeApproximately(1000.0, 0.001);
        options.PreserveMetadata.Should().BeTrue();
    }

    [Fact]
    public void TonemapAlgorithm_HasAllExpectedValues()
    {
        TonemapAlgorithm[] values = Enum.GetValues<TonemapAlgorithm>();

        values.Should().Contain(TonemapAlgorithm.Hable);
        values.Should().Contain(TonemapAlgorithm.Reinhard);
        values.Should().Contain(TonemapAlgorithm.Mobius);
        values.Should().Contain(TonemapAlgorithm.Bt2390);
        values.Should().HaveCount(4);
    }

    [Fact]
    public void LutApplication_HasAllExpectedValues()
    {
        LutApplication[] values = Enum.GetValues<LutApplication>();

        values.Should().Contain(LutApplication.BeforeTonemap);
        values.Should().Contain(LutApplication.AfterTonemap);
        values.Should().Contain(LutApplication.InsteadOfTonemap);
        values.Should().HaveCount(3);
    }

    [Fact]
    public void ZscaleFilterChain_ContainsTonemap()
    {
        _ffmpegMock.Setup(f => f.HasFilter("libplacebo")).Returns(false);
        _ffmpegMock.Setup(f => f.HasFilter("tonemap_opencl")).Returns(false);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(_hardwareMock.Object, _ffmpegMock.Object);

        strategy.FfmpegFilterChain.Should().Contain("tonemap=tonemap=hable");
        strategy.FfmpegFilterChain.Should().Contain("yuv420p");
    }
}
