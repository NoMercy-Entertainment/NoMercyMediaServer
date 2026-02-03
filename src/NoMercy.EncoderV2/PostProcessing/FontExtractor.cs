using Newtonsoft.Json;
using NoMercy.Encoder;
using NoMercy.Encoder.Dto;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.EncoderV2.PostProcessing;

/// <summary>
/// Extracted font information
/// </summary>
public class ExtractedFont
{
    [JsonProperty("file")] public string FilePath { get; set; } = string.Empty;
    [JsonProperty("filename")] public string Filename { get; set; } = string.Empty;
    [JsonProperty("mimeType")] public string MimeType { get; set; } = string.Empty;
    [JsonProperty("size")] public long Size { get; set; }
    [JsonProperty("sourceIndex")] public int SourceIndex { get; set; }
}

/// <summary>
/// Result of font extraction operation
/// </summary>
public class FontExtractionResult
{
    public bool Success { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public string ManifestPath { get; set; } = string.Empty;
    public List<ExtractedFont> Fonts { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int TotalFontsExtracted => Fonts.Count;
}

/// <summary>
/// Interface for font extraction from media files
/// </summary>
public interface IFontExtractor
{
    /// <summary>
    /// Extracts all embedded fonts from a media file
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="outputDirectory">Directory where fonts and manifest will be written</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with font information</returns>
    Task<FontExtractionResult> ExtractFontsAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a media file contains embedded fonts
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if fonts are present</returns>
    Task<bool> HasFontsAsync(string inputFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata about embedded fonts without extracting them
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of font attachments</returns>
    Task<List<Attachment>> GetFontMetadataAsync(string inputFilePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extracts embedded fonts from media files (MKV, ASS/SSA containers)
/// Generates fonts.json manifest with MIME types for web delivery
/// </summary>
public class FontExtractor : IFontExtractor
{
    private const string FontsFolder = "fonts";
    private const string ManifestFilename = "fonts.json";

    private static readonly HashSet<string> FontMimeTypes =
    [
        "application/x-font-truetype",
        "application/x-font-opentype",
        "application/font-woff",
        "application/font-woff2",
        "application/vnd.ms-fontobject",
        "font/ttf",
        "font/otf",
        "font/woff",
        "font/woff2"
    ];

    private static readonly HashSet<string> FontExtensions =
    [
        ".ttf",
        ".otf",
        ".woff",
        ".woff2",
        ".eot"
    ];

    public async Task<FontExtractionResult> ExtractFontsAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        FontExtractionResult result = new()
        {
            OutputDirectory = Path.Combine(outputDirectory, FontsFolder),
            ManifestPath = Path.Combine(outputDirectory, ManifestFilename)
        };

        try
        {
            if (!File.Exists(inputFilePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Input file not found: {inputFilePath}";
                return result;
            }

            // Get font metadata first to check if there are fonts to extract
            List<Attachment> fontAttachments = await GetFontMetadataAsync(inputFilePath, cancellationToken);

            if (fontAttachments.Count == 0)
            {
                result.Success = true;
                return result;
            }

            // Create fonts directory
            if (!Directory.Exists(result.OutputDirectory))
            {
                Directory.CreateDirectory(result.OutputDirectory);
            }

            // Extract fonts using FFmpeg
            string command = $@"-dump_attachment:t """" -i ""{inputFilePath}"" -y -hide_banner -t 0 -f null null";

            await Shell.ExecAsync(
                AppFiles.FfmpegPath,
                command,
                new Shell.ExecOptions { WorkingDirectory = result.OutputDirectory });

            // Process extracted files
            string[] extractedFiles = Directory.GetFiles(result.OutputDirectory);

            foreach (string file in extractedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string filename = Path.GetFileName(file);
                string mimeType = MimeTypes.GetMimeTypeFromFile(file);
                FileInfo fileInfo = new(file);

                // Find matching attachment for source index
                Attachment? matchingAttachment = fontAttachments
                    .FirstOrDefault(a => a.Filename == filename);

                result.Fonts.Add(new ExtractedFont
                {
                    FilePath = $"{FontsFolder}/{filename}",
                    Filename = filename,
                    MimeType = mimeType,
                    Size = fileInfo.Length,
                    SourceIndex = matchingAttachment?.Index ?? -1
                });
            }

            // Write manifest
            await WriteFontManifestAsync(result.ManifestPath, result.Fonts, cancellationToken);

            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Font extraction was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to extract fonts: {ex.Message}";
        }

        return result;
    }

    public async Task<bool> HasFontsAsync(string inputFilePath, CancellationToken cancellationToken = default)
    {
        List<Attachment> attachments = await GetFontMetadataAsync(inputFilePath, cancellationToken);
        return attachments.Count > 0;
    }

    public async Task<List<Attachment>> GetFontMetadataAsync(
        string inputFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputFilePath))
        {
            return [];
        }

        Ffprobe ffprobe = new(inputFilePath);
        await ffprobe.GetStreamData();

        // Filter attachments to only font types
        List<Attachment> fontAttachments = ffprobe.Attachments
            .Where(IsFontAttachment)
            .ToList();

        return fontAttachments;
    }

    private static bool IsFontAttachment(Attachment attachment)
    {
        // Check by MIME type
        if (!string.IsNullOrEmpty(attachment.Mimetype))
        {
            string mimeType = attachment.Mimetype.ToLowerInvariant();
            if (FontMimeTypes.Contains(mimeType))
            {
                return true;
            }
        }

        // Check by filename extension
        if (!string.IsNullOrEmpty(attachment.Filename))
        {
            string extension = Path.GetExtension(attachment.Filename).ToLowerInvariant();
            if (FontExtensions.Contains(extension))
            {
                return true;
            }
        }

        // Check by codec name (FFmpeg uses specific codec names for fonts)
        if (!string.IsNullOrEmpty(attachment.CodecName))
        {
            string codecName = attachment.CodecName.ToLowerInvariant();
            if (codecName is "ttf" or "otf")
            {
                return true;
            }
        }

        return false;
    }

    private static async Task WriteFontManifestAsync(
        string manifestPath,
        List<ExtractedFont> fonts,
        CancellationToken cancellationToken)
    {
        string json = fonts.ToJson();
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }
}
