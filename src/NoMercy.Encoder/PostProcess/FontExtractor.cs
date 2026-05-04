using System.Text;
using Newtonsoft.Json;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Commands;
using NoMercy.Storage;

namespace NoMercy.Encoder.PostProcess;

public class FontExtractor(IStorage storage) : IFontExtractor
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
        string fontDir = storage.CombinePath(outputDirectory, "fonts");

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

        return new(ffmpegPath, args, fontDir);
    }

    public async Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct)
    {
        string fontDir = storage.CombinePath(outputDirectory, "fonts");

        if (!storage.Exists(fontDir))
            return;

        IReadOnlyList<StorageEntry> fontFiles = storage
            .List(fontDir, "*", recursive: false)
            .Where(e => !e.IsDirectory)
            .ToList();

        if (fontFiles.Count == 0)
        {
            storage.DeleteDirectory(fontDir, recursive: false);
            return;
        }

        List<FontEntry> entries = fontFiles
            .Select(f => new FontEntry(
                File: $"fonts/{storage.GetName(f.Path)}",
                MimeType: GetFontMimeType(f.Path)
            ))
            .ToList();

        string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
        await storage.WriteAsync(
            storage.CombinePath(outputDirectory, "fonts.json"),
            Encoding.UTF8.GetBytes(json),
            ct
        );
    }

    private static string GetFontMimeType(string path)
    {
        int dot = path.LastIndexOf('.');
        string ext = dot < 0 ? string.Empty : path[dot..].ToLowerInvariant();
        return ext switch
        {
            ".ttf" => "application/x-font-truetype",
            ".otf" => "application/x-font-opentype",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };
    }

    private record FontEntry(
        [property: JsonProperty("file")] string File,
        [property: JsonProperty("mimeType")] string MimeType
    );
}
