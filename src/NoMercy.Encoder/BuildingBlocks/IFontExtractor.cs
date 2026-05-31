using NoMercy.Encoder.Commands;

namespace NoMercy.Encoder.BuildingBlocks;

public interface IFontExtractor
{
    FfmpegCommand BuildExtractionCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory
    );

    Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct);
}
