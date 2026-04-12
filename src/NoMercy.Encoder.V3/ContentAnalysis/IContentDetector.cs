namespace NoMercy.Encoder.V3.ContentAnalysis;

public interface IContentDetector
{
    Task<ContentSegment[]> DetectSegmentsAsync(string inputPath, CancellationToken ct);
}

public record ContentSegment(
    TimeSpan Start,
    TimeSpan End,
    ContentSegmentType Type,
    double Confidence
);

public enum ContentSegmentType
{
    Intro,
    Outro,
    Commercial,
    Credits,
    MainContent,
}
