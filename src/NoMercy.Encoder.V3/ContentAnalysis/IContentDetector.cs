namespace NoMercy.Encoder.V3.ContentAnalysis;

public interface IContentDetector
{
    Task<ContentSegment[]> DetectAsync(string inputPath, CancellationToken ct);

    Task<ContentSegment[]> DetectIntroOutroAsync(string[] episodePaths, CancellationToken ct);
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
    Recap,
    Content,
}
