namespace NoMercy.Tests.Encoder.ContentAnalysis;

using NoMercy.Encoder.Subtitles;

/// <summary>
/// Unit tests for <see cref="OcrImagePreprocessor.PreprocessForOcr"/>.
/// No file I/O, no FFmpeg — pure pixel math.
/// </summary>
public class OcrImagePreprocessorTests
{
    [Fact]
    public void PreprocessForOcr_FullyOpaque_ReturnsLumaOfOriginalColor()
    {
        // Fully-opaque white pixel (RGBA = 255,255,255,255)
        // BT.601 luma of white = 255; alpha=1 → output = 1*255 + 0*128 = 255
        byte[] rgba = [255, 255, 255, 255];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result.Should().HaveCount(1);
        result[0].Should().Be(255);
    }

    [Fact]
    public void PreprocessForOcr_FullyTransparent_ReturnsNeutralGrey()
    {
        // Fully-transparent pixel → output = 0*luma + 1*128 = 128
        byte[] rgba = [0, 0, 0, 0];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result.Should().HaveCount(1);
        result[0].Should().Be(128);
    }

    [Fact]
    public void PreprocessForOcr_HalfAlpha_BlendsBetweenLumaAndGrey()
    {
        // Black pixel (luma=0) at alpha=128 (~50%)
        // out = 0.502 * 0 + 0.498 * 128 ≈ 63.7 → 64
        byte[] rgba = [0, 0, 0, 128];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result.Should().HaveCount(1);
        // Allow ±2 for floating-point rounding
        result[0].Should().BeInRange(62, 66);
    }

    [Fact]
    public void PreprocessForOcr_OutputLengthMatchesWidthTimesHeight()
    {
        int w = 4;
        int h = 3;
        byte[] rgba = new byte[w * h * 4]; // all zeros
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, w, h);

        result.Should().HaveCount(w * h);
    }

    [Fact]
    public void PreprocessForOcr_WrongBufferSize_Throws()
    {
        // 2x2 image needs 16 bytes; supply 12
        byte[] tooShort = new byte[12];
        Action act = () => OcrImagePreprocessor.PreprocessForOcr(tooShort, 2, 2);

        act.Should().Throw<ArgumentException>().WithMessage("*16*");
    }

    [Fact]
    public void PreprocessForOcr_FullyOpaqueBlack_ReturnsZero()
    {
        // Black, fully opaque → luma=0, alpha=1 → out = 0
        byte[] rgba = [0, 0, 0, 255];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result[0].Should().Be(0);
    }

    [Fact]
    public void PreprocessForOcr_MultiPixel_AllTransparent_AllReturnGrey()
    {
        int w = 8;
        int h = 8;
        byte[] rgba = new byte[w * h * 4]; // all zeros → alpha=0
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, w, h);

        result.Should().AllSatisfy(b => b.Should().Be(128));
    }
}
