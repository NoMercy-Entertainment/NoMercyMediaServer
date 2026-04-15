namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Analysis;

public interface IChapterWriter
{
    Task WriteChaptersAsync(
        string outputDirectory,
        IReadOnlyList<ChapterInfo> chapters,
        CancellationToken ct
    );
}
