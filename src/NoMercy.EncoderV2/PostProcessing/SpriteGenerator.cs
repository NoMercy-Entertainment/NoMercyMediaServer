using System.Text;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using SixLabors.ImageSharp;

namespace NoMercy.EncoderV2.PostProcessing;

/// <summary>
/// Individual sprite frame information
/// </summary>
public class SpriteFrame
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("timestamp")] public double Timestamp { get; set; }
    [JsonProperty("x")] public int X { get; set; }
    [JsonProperty("y")] public int Y { get; set; }
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
}

/// <summary>
/// Sprite sheet metadata
/// </summary>
public class SpriteMetadata
{
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("frameWidth")] public int FrameWidth { get; set; }
    [JsonProperty("frameHeight")] public int FrameHeight { get; set; }
    [JsonProperty("gridColumns")] public int GridColumns { get; set; }
    [JsonProperty("gridRows")] public int GridRows { get; set; }
    [JsonProperty("frameCount")] public int FrameCount { get; set; }
    [JsonProperty("intervalSeconds")] public double IntervalSeconds { get; set; }
    [JsonProperty("totalDuration")] public double TotalDuration { get; set; }
    [JsonProperty("frames")] public List<SpriteFrame> Frames { get; set; } = [];
}

/// <summary>
/// Result of sprite generation operation
/// </summary>
public class SpriteGenerationResult
{
    public bool Success { get; set; }
    public string SpriteFilePath { get; set; } = string.Empty;
    public string VttFilePath { get; set; } = string.Empty;
    public string ThumbnailsDirectory { get; set; } = string.Empty;
    public SpriteMetadata? Metadata { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalFrames => Metadata?.FrameCount ?? 0;
}

/// <summary>
/// Interface for sprite sheet generation from media files
/// </summary>
public interface ISpriteGenerator
{
    /// <summary>
    /// Generates thumbnail sprite sheet and VTT timing file from a media file
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="outputDirectory">Directory where sprite and VTT will be written</param>
    /// <param name="width">Thumbnail width</param>
    /// <param name="height">Thumbnail height (null to preserve aspect ratio)</param>
    /// <param name="intervalSeconds">Interval between thumbnails in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generation result with sprite information</returns>
    Task<SpriteGenerationResult> GenerateSpriteAsync(
        string inputFilePath,
        string outputDirectory,
        int width = 320,
        int? height = null,
        double intervalSeconds = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates sprite sheet from pre-extracted thumbnail files
    /// </summary>
    /// <param name="thumbnailsDirectory">Directory containing thumbnail images</param>
    /// <param name="outputDirectory">Directory where sprite and VTT will be written</param>
    /// <param name="intervalSeconds">Interval between thumbnails in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generation result with sprite information</returns>
    Task<SpriteGenerationResult> GenerateSpriteFromThumbnailsAsync(
        string thumbnailsDirectory,
        string outputDirectory,
        double intervalSeconds = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the estimated duration of the media file in seconds
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Duration in seconds</returns>
    Task<double> GetDurationAsync(string inputFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates sprite metadata without generating files
    /// </summary>
    /// <param name="durationSeconds">Total duration in seconds</param>
    /// <param name="intervalSeconds">Interval between thumbnails</param>
    /// <param name="frameWidth">Width of each thumbnail</param>
    /// <param name="frameHeight">Height of each thumbnail</param>
    /// <returns>Calculated sprite metadata</returns>
    SpriteMetadata CalculateSpriteMetadata(
        double durationSeconds,
        double intervalSeconds,
        int frameWidth,
        int frameHeight);
}

/// <summary>
/// Generates thumbnail sprite sheets and WebVTT timing files from media files.
/// Creates individual thumbnails at specified intervals, combines them into a single sprite sheet,
/// and generates a VTT file with timing cues for video player scrubbing preview.
/// </summary>
public class SpriteGenerator : ISpriteGenerator
{
    private const string ThumbnailsFolderPrefix = "thumbs_";
    private const string SpriteExtension = ".webp";
    private const string VttExtension = ".vtt";
    private const string ThumbnailPattern = "%04d.jpg";

    public async Task<SpriteGenerationResult> GenerateSpriteAsync(
        string inputFilePath,
        string outputDirectory,
        int width = 320,
        int? height = null,
        double intervalSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        SpriteGenerationResult result = new();

        try
        {
            if (!File.Exists(inputFilePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Input file not found: {inputFilePath}";
                return result;
            }

            // Create output directory if needed
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Get video duration
            double duration = await GetDurationAsync(inputFilePath, cancellationToken);
            if (duration <= 0)
            {
                result.Success = false;
                result.ErrorMessage = "Could not determine video duration";
                return result;
            }

            // Calculate height if not specified (preserve aspect ratio)
            int effectiveHeight = height ?? -1;

            // Build folder and file names
            string dimensionSuffix = effectiveHeight > 0 ? $"{width}x{effectiveHeight}" : $"{width}x-1";
            string baseName = $"{ThumbnailsFolderPrefix}{dimensionSuffix}";
            string thumbnailsFolder = Path.Combine(outputDirectory, baseName);
            string spriteFile = Path.Combine(outputDirectory, baseName + SpriteExtension);
            string vttFile = Path.Combine(outputDirectory, baseName + VttExtension);

            result.ThumbnailsDirectory = thumbnailsFolder;
            result.SpriteFilePath = spriteFile;
            result.VttFilePath = vttFile;

            // Create thumbnails directory
            if (!Directory.Exists(thumbnailsFolder))
            {
                Directory.CreateDirectory(thumbnailsFolder);
            }

            // Extract thumbnails using FFmpeg
            cancellationToken.ThrowIfCancellationRequested();
            await ExtractThumbnailsAsync(inputFilePath, thumbnailsFolder, baseName, width, effectiveHeight, intervalSeconds, cancellationToken);

            // Get thumbnail files
            string[] thumbnailFiles = Directory.GetFiles(thumbnailsFolder, "*.jpg")
                .OrderBy(f => f)
                .ToArray();

            if (thumbnailFiles.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No thumbnails were generated";
                return result;
            }

            // Get actual thumbnail dimensions from first file
            (int frameWidth, int frameHeight) = GetImageDimensions(thumbnailFiles[0]);

            // Calculate sprite grid
            int gridColumns = (int)Math.Ceiling(Math.Sqrt(thumbnailFiles.Length));
            int gridRows = (int)Math.Ceiling((double)thumbnailFiles.Length / gridColumns);

            // Generate sprite sheet
            cancellationToken.ThrowIfCancellationRequested();
            await GenerateSpriteSheetAsync(thumbnailsFolder, baseName, spriteFile, gridColumns, gridRows, cancellationToken);

            // Generate VTT file
            cancellationToken.ThrowIfCancellationRequested();
            SpriteMetadata metadata = await GenerateVttFileAsync(
                vttFile,
                spriteFile,
                thumbnailFiles.Length,
                intervalSeconds,
                frameWidth,
                frameHeight,
                gridColumns,
                gridRows,
                cancellationToken);

            result.Metadata = metadata;

            // Clean up individual thumbnails
            if (Directory.Exists(thumbnailsFolder))
            {
                Directory.Delete(thumbnailsFolder, true);
            }

            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Sprite generation was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to generate sprite: {ex.Message}";
        }

        return result;
    }

    public async Task<SpriteGenerationResult> GenerateSpriteFromThumbnailsAsync(
        string thumbnailsDirectory,
        string outputDirectory,
        double intervalSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        SpriteGenerationResult result = new()
        {
            ThumbnailsDirectory = thumbnailsDirectory
        };

        try
        {
            if (!Directory.Exists(thumbnailsDirectory))
            {
                result.Success = false;
                result.ErrorMessage = $"Thumbnails directory not found: {thumbnailsDirectory}";
                return result;
            }

            // Get thumbnail files
            string[] thumbnailFiles = Directory.GetFiles(thumbnailsDirectory, "*.jpg")
                .OrderBy(f => f)
                .ToArray();

            if (thumbnailFiles.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No thumbnail files found in directory";
                return result;
            }

            // Create output directory if needed
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Get dimensions from directory name or first thumbnail
            string folderName = Path.GetFileName(thumbnailsDirectory);
            (int frameWidth, int frameHeight) = GetImageDimensions(thumbnailFiles[0]);

            // Build output file names
            string baseName = folderName.StartsWith(ThumbnailsFolderPrefix)
                ? folderName
                : $"{ThumbnailsFolderPrefix}{frameWidth}x{frameHeight}";
            string spriteFile = Path.Combine(outputDirectory, baseName + SpriteExtension);
            string vttFile = Path.Combine(outputDirectory, baseName + VttExtension);

            result.SpriteFilePath = spriteFile;
            result.VttFilePath = vttFile;

            // Calculate sprite grid
            int gridColumns = (int)Math.Ceiling(Math.Sqrt(thumbnailFiles.Length));
            int gridRows = (int)Math.Ceiling((double)thumbnailFiles.Length / gridColumns);

            // Generate sprite sheet
            cancellationToken.ThrowIfCancellationRequested();
            await GenerateSpriteSheetAsync(thumbnailsDirectory, baseName, spriteFile, gridColumns, gridRows, cancellationToken);

            // Generate VTT file
            cancellationToken.ThrowIfCancellationRequested();
            SpriteMetadata metadata = await GenerateVttFileAsync(
                vttFile,
                spriteFile,
                thumbnailFiles.Length,
                intervalSeconds,
                frameWidth,
                frameHeight,
                gridColumns,
                gridRows,
                cancellationToken);

            result.Metadata = metadata;

            // Clean up individual thumbnails
            if (Directory.Exists(thumbnailsDirectory))
            {
                Directory.Delete(thumbnailsDirectory, true);
            }

            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Sprite generation was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to generate sprite from thumbnails: {ex.Message}";
        }

        return result;
    }

    public async Task<double> GetDurationAsync(string inputFilePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(inputFilePath))
        {
            return 0;
        }

        string command = $"-v quiet -print_format json -show_format \"{inputFilePath}\"";
        string result = await Shell.ExecStdOutAsync(AppFiles.FfProbePath, command);

        if (string.IsNullOrWhiteSpace(result))
        {
            return 0;
        }

        try
        {
            FfprobeDurationRoot? durationRoot = JsonConvert.DeserializeObject<FfprobeDurationRoot>(result);
            if (durationRoot?.Format?.Duration != null &&
                double.TryParse(durationRoot.Format.Duration, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double duration))
            {
                return duration;
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return 0;
    }

    public SpriteMetadata CalculateSpriteMetadata(
        double durationSeconds,
        double intervalSeconds,
        int frameWidth,
        int frameHeight)
    {
        int frameCount = (int)Math.Ceiling(durationSeconds / intervalSeconds);
        int gridColumns = (int)Math.Ceiling(Math.Sqrt(frameCount));
        int gridRows = (int)Math.Ceiling((double)frameCount / gridColumns);

        SpriteMetadata metadata = new()
        {
            Width = gridColumns * frameWidth,
            Height = gridRows * frameHeight,
            FrameWidth = frameWidth,
            FrameHeight = frameHeight,
            GridColumns = gridColumns,
            GridRows = gridRows,
            FrameCount = frameCount,
            IntervalSeconds = intervalSeconds,
            TotalDuration = durationSeconds
        };

        // Calculate frame positions
        int frameIndex = 0;
        for (int row = 0; row < gridRows && frameIndex < frameCount; row++)
        {
            for (int col = 0; col < gridColumns && frameIndex < frameCount; col++)
            {
                metadata.Frames.Add(new SpriteFrame
                {
                    Index = frameIndex + 1,
                    Timestamp = frameIndex * intervalSeconds,
                    X = col * frameWidth,
                    Y = row * frameHeight,
                    Width = frameWidth,
                    Height = frameHeight
                });
                frameIndex++;
            }
        }

        return metadata;
    }

    private static async Task ExtractThumbnailsAsync(
        string inputFilePath,
        string outputDirectory,
        string baseName,
        int width,
        int height,
        double intervalSeconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Build FFmpeg command for thumbnail extraction
        // -vf fps=1/{interval}: Extract one frame per interval
        // -vf scale=width:height: Scale to specified dimensions (-1 preserves aspect ratio)
        string scaleFilter = height > 0 ? $"scale={width}:{height}" : $"scale={width}:-1";
        string outputPattern = Path.Combine(outputDirectory, $"{baseName}-{ThumbnailPattern}");

        StringBuilder command = new();
        command.Append($"-i \"{inputFilePath}\" ");
        command.Append($"-vf \"fps=1/{intervalSeconds},{scaleFilter}\" ");
        command.Append($"-q:v 5 "); // JPEG quality (2-31, lower is better)
        command.Append($"-y \"{outputPattern}\"");

        await Shell.ExecAsync(
            AppFiles.FfmpegPath,
            command.ToString(),
            new Shell.ExecOptions { WorkingDirectory = outputDirectory });
    }

    private static async Task GenerateSpriteSheetAsync(
        string thumbnailsDirectory,
        string baseName,
        string outputFile,
        int gridColumns,
        int gridRows,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Input pattern for sequential thumbnails
        string inputPattern = Path.Combine(thumbnailsDirectory, $"{baseName}-{ThumbnailPattern}");

        // Build FFmpeg command for sprite sheet generation
        // tile filter combines images into a grid
        StringBuilder command = new();
        command.Append($"-i \"{inputPattern}\" ");
        command.Append($"-filter_complex tile=\"{gridColumns}x{gridRows}\" ");
        command.Append($"-y \"{outputFile}\"");

        await Shell.ExecAsync(
            AppFiles.FfmpegPath,
            command.ToString(),
            new Shell.ExecOptions { WorkingDirectory = Path.GetDirectoryName(outputFile) ?? "." });
    }

    private static async Task<SpriteMetadata> GenerateVttFileAsync(
        string vttFilePath,
        string spriteFilePath,
        int frameCount,
        double intervalSeconds,
        int frameWidth,
        int frameHeight,
        int gridColumns,
        int gridRows,
        CancellationToken cancellationToken)
    {
        string spriteFilename = Path.GetFileName(spriteFilePath);

        SpriteMetadata metadata = new()
        {
            Width = gridColumns * frameWidth,
            Height = gridRows * frameHeight,
            FrameWidth = frameWidth,
            FrameHeight = frameHeight,
            GridColumns = gridColumns,
            GridRows = gridRows,
            FrameCount = frameCount,
            IntervalSeconds = intervalSeconds,
            TotalDuration = frameCount * intervalSeconds
        };

        await using StreamWriter writer = new(vttFilePath);

        // WebVTT header
        await writer.WriteLineAsync("WEBVTT");
        await writer.WriteLineAsync();

        int frameIndex = 0;
        int x = 0;
        int y = 0;

        for (int row = 0; row < gridRows && frameIndex < frameCount; row++)
        {
            for (int col = 0; col < gridColumns && frameIndex < frameCount; col++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double startTime = frameIndex * intervalSeconds;
                double endTime = (frameIndex + 1) * intervalSeconds;

                // Track frame position in metadata
                metadata.Frames.Add(new SpriteFrame
                {
                    Index = frameIndex + 1,
                    Timestamp = startTime,
                    X = x,
                    Y = y,
                    Width = frameWidth,
                    Height = frameHeight
                });

                // Cue identifier
                await writer.WriteLineAsync((frameIndex + 1).ToString());

                // Timestamp line: start --> end (format: hh:mm:ss.fff)
                await writer.WriteLineAsync($"{startTime.ToHis()} --> {endTime.ToHis()}");

                // Sprite reference with xywh fragment
                await writer.WriteLineAsync($"{spriteFilename}#xywh={x},{y},{frameWidth},{frameHeight}");

                // Empty line to separate cues
                await writer.WriteLineAsync();

                x += frameWidth;
                frameIndex++;
            }

            // Move to next row
            x = 0;
            y += frameHeight;
        }

        return metadata;
    }

    private static (int width, int height) GetImageDimensions(string imagePath)
    {
        using Image image = Image.Load(imagePath);
        return (image.Width, image.Height);
    }
}

#region FFprobe Duration DTOs

/// <summary>
/// Root object for FFprobe format JSON output
/// </summary>
internal class FfprobeDurationRoot
{
    [JsonProperty("format")] public FfprobeDurationFormat? Format { get; set; }
}

/// <summary>
/// Format info from FFprobe output
/// </summary>
internal class FfprobeDurationFormat
{
    [JsonProperty("duration")] public string? Duration { get; set; }
}

#endregion
