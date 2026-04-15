namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;

public interface IThumbnailGenerator
{
    FfmpegCommand BuildCaptureCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        TimeSpan duration
    );

    FfmpegCommand BuildSpriteCommand(
        string ffmpegPath,
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount
    );

    Task WriteVttCueFileAsync(
        string outputDirectory,
        ThumbnailOutputPlan plan,
        int imageCount,
        TimeSpan duration,
        CancellationToken ct
    );

    void CleanupIndividualThumbnails(string outputDirectory, ThumbnailOutputPlan plan);
}
