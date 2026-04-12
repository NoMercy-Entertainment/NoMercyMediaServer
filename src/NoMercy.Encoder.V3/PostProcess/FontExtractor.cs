namespace NoMercy.Encoder.V3.PostProcess;

using Newtonsoft.Json;
using NoMercy.Encoder.V3.Commands;

public class FontExtractor
{
    // FFmpeg dumps attachments via -dump_attachment:t "" which is a pre-input flag.
    // The standard builder does not model pre-input attachment flags, so we build
    // the argument list directly to keep the contract explicit and simple.
    public FfmpegCommand BuildExtractionCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory
    )
    {
        string fontDir = Path.Combine(outputDirectory, "fonts");

        string[] args =
        [
            "-y",
            "-hide_banner",
            "-dump_attachment:t",
            "",
            "-i",
            inputPath,
            "-f",
            "null",
            "-",
        ];

        return new FfmpegCommand(ffmpegPath, args, fontDir);
    }

    public async Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct)
    {
        string fontDir = Path.Combine(outputDirectory, "fonts");

        if (!Directory.Exists(fontDir))
            return;

        string[] fontFiles = Directory.GetFiles(fontDir);

        if (fontFiles.Length == 0)
        {
            Directory.Delete(fontDir);
            return;
        }

        List<FontEntry> entries = fontFiles
            .Select(f => new FontEntry(
                File: $"fonts/{Path.GetFileName(f)}",
                MimeType: GetFontMimeType(f)
            ))
            .ToList();

        string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "fonts.json"), json, ct);
    }

    private static string GetFontMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".ttf" => "font/ttf",
            ".otf" => "font/otf",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };

    private record FontEntry(string File, string MimeType);
}
