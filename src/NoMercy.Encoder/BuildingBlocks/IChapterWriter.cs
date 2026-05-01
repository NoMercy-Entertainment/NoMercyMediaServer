using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.BuildingBlocks;

public interface IChapterWriter
{
    Task WriteChaptersAsync(
        string outputDirectory,
        IReadOnlyList<ChapterInfo> chapters,
        CancellationToken ct
    );
}
