using NoMercy.Encoder.ContentAnalysis;

namespace NoMercy.Tests.Encoder.AdvancedFeatures;

public class ContentAnalysisTests
{
    [Fact]
    public void ContentSegmentType_HasExpectedValues()
    {
        ContentSegmentType[] values = Enum.GetValues<ContentSegmentType>();

        values.Should().Contain(ContentSegmentType.Intro);
        values.Should().Contain(ContentSegmentType.Outro);
        values.Should().Contain(ContentSegmentType.Commercial);
        values.Should().Contain(ContentSegmentType.Recap);
        values.Should().Contain(ContentSegmentType.Content);
        values.Should().HaveCount(5);
    }

    [Fact]
    public void ContentSegment_ConstructsCorrectly()
    {
        TimeSpan start = TimeSpan.FromSeconds(5);
        TimeSpan end = TimeSpan.FromSeconds(95);

        ContentSegment segment = new(
            Start: start,
            End: end,
            Type: ContentSegmentType.Intro,
            Confidence: 0.92
        );

        segment.Start.Should().Be(start);
        segment.End.Should().Be(end);
        segment.Type.Should().Be(ContentSegmentType.Intro);
        segment.Confidence.Should().BeApproximately(0.92, 0.001);
    }

    [Fact]
    public void ContentSegment_Content_HasHighConfidence()
    {
        ContentSegment segment = new(
            Start: TimeSpan.FromSeconds(90),
            End: TimeSpan.FromSeconds(3600),
            Type: ContentSegmentType.Content,
            Confidence: 0.99
        );

        segment.Type.Should().Be(ContentSegmentType.Content);
        segment.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void ContentSegment_Recap_AtEndOfFile()
    {
        ContentSegment segment = new(
            Start: TimeSpan.FromSeconds(3500),
            End: TimeSpan.FromSeconds(3700),
            Type: ContentSegmentType.Recap,
            Confidence: 0.88
        );

        segment.Type.Should().Be(ContentSegmentType.Recap);
        segment.End.Should().BeGreaterThan(segment.Start);
    }

    [Fact]
    public void CropResult_NoCrop_HasShouldCropFalse()
    {
        CropResult result = new(Width: 1920, Height: 1080, X: 0, Y: 0, ShouldCrop: false);

        result.Width.Should().Be(1920);
        result.Height.Should().Be(1080);
        result.X.Should().Be(0);
        result.Y.Should().Be(0);
        result.ShouldCrop.Should().BeFalse();
    }

    [Fact]
    public void CropResult_WithLetterbox_HasShouldCropTrue()
    {
        CropResult result = new(Width: 1920, Height: 800, X: 0, Y: 140, ShouldCrop: true);

        result.ShouldCrop.Should().BeTrue();
        result.Y.Should().Be(140);
        result.Height.Should().Be(800);
    }

    [Fact]
    public void CropResult_WithPillarbox_HasShouldCropTrue()
    {
        CropResult result = new(Width: 1440, Height: 1080, X: 240, Y: 0, ShouldCrop: true);

        result.ShouldCrop.Should().BeTrue();
        result.X.Should().Be(240);
        result.Width.Should().Be(1440);
    }

    [Fact]
    public void CropResult_ZeroOffset_IsValid()
    {
        CropResult result = new(Width: 0, Height: 0, X: 0, Y: 0, ShouldCrop: false);

        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
        result.ShouldCrop.Should().BeFalse();
    }
}
