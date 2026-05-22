using NoMercy.Encoder.Core;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class LinearQualityScalerTests
{
    private readonly LinearQualityScaler _scaler = new();

    // Examples from The Effortless Encoder journal, part 02:
    // - CRF 23 on H.264 (sourceMax=51) → VideoToolbox (targetMax=100) = round(23/51*100) = 45
    // - CRF * 255 / 63 = AMF AV1 QP

    [Theory]
    [InlineData(23, 51, 100, 45)] // journal example: H.264 CRF 23 → VideoToolbox 45
    [InlineData(0, 51, 100, 0)]
    [InlineData(51, 51, 100, 100)] // boundary
    [InlineData(28, 51, 100, 55)] // round((28/51)*100) = 54.9 → 55
    public void Translate_H264CrfToVideoToolbox(
        int sourceCrf,
        int sourceMax,
        int targetMax,
        int expected
    )
    {
        Assert.Equal(
            expected,
            _scaler.Translate(sourceCrf, sourceMax, targetMax, CodecHint.VideoToolboxH264)
        );
    }

    [Theory]
    [InlineData(23, 63, 255, 93)] // journal example: AV1 CRF 23 → AMF AV1 QP 93
    [InlineData(0, 63, 255, 0)]
    [InlineData(63, 63, 255, 255)]
    public void Translate_Av1CrfToAmfAv1Qp(
        int sourceCrf,
        int sourceMax,
        int targetMax,
        int expected
    )
    {
        Assert.Equal(
            expected,
            _scaler.Translate(sourceCrf, sourceMax, targetMax, CodecHint.AmfAv1)
        );
    }

    [Theory]
    [InlineData(0, 51, 51, CodecHint.QsvH264, 1)] // QSV clamps lower bound
    [InlineData(1, 51, 51, CodecHint.QsvH264, 1)]
    [InlineData(23, 51, 51, CodecHint.QsvH264, 23)]
    [InlineData(0, 51, 51, CodecHint.QsvHevc, 1)] // same for HEVC QSV
    [InlineData(0, 51, 51, CodecHint.SoftwareH264, 0)] // software accepts 0
    public void Translate_QsvClampsLowerBoundToOne(
        int sourceCrf,
        int sourceMax,
        int targetMax,
        CodecHint hint,
        int expected
    )
    {
        Assert.Equal(expected, _scaler.Translate(sourceCrf, sourceMax, targetMax, hint));
    }

    [Theory]
    [InlineData(23, 51, 51, CodecHint.NvencH264, 23)] // identity range
    [InlineData(23, 51, 51, CodecHint.SoftwareHevc, 23)]
    [InlineData(23, 51, 51, CodecHint.NvencAv1, 23)]
    public void Translate_IdentityWhenScalesMatch(
        int sourceCrf,
        int sourceMax,
        int targetMax,
        CodecHint hint,
        int expected
    )
    {
        Assert.Equal(expected, _scaler.Translate(sourceCrf, sourceMax, targetMax, hint));
    }

    [Fact]
    public void Translate_NegativeSourceClampsToZero()
    {
        Assert.Equal(0, _scaler.Translate(-5, 51, 100, CodecHint.SoftwareH264));
    }

    [Fact]
    public void Translate_ResultCannotExceedTargetMax()
    {
        Assert.Equal(100, _scaler.Translate(999, 51, 100, CodecHint.SoftwareH264));
    }

    [Fact]
    public void Translate_ZeroSourceMaxReturnsZero()
    {
        Assert.Equal(0, _scaler.Translate(23, 0, 100, CodecHint.SoftwareH264));
    }

    [Fact]
    public void Translate_RoundsHalfAwayFromZero()
    {
        // 25 / 51 * 100 = 49.0196... → 49
        Assert.Equal(49, _scaler.Translate(25, 51, 100, CodecHint.VideoToolboxH264));
        // 26 / 51 * 100 = 50.98 → 51
        Assert.Equal(51, _scaler.Translate(26, 51, 100, CodecHint.VideoToolboxH264));
    }
}
