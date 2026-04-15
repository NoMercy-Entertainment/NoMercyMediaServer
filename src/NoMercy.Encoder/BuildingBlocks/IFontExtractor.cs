namespace NoMercy.Encoder.BuildingBlocks;

using NoMercy.Encoder.Commands;

public interface IFontExtractor
{
    FfmpegCommand BuildExtractionCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory
    );

    Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct);
}
