using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.EncoderV2.PostProcessing;

/// <summary>
/// Chapter information extracted from media file
/// </summary>
public class ChapterInfo
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("startTime")] public double StartTime { get; set; }
    [JsonProperty("endTime")] public double EndTime { get; set; }
}

/// <summary>
/// Result of chapter extraction operation
/// </summary>
public class ChapterExtractionResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public List<ChapterInfo> Chapters { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int TotalChapters => Chapters.Count;
}

/// <summary>
/// Interface for chapter extraction and WebVTT generation
/// </summary>
public interface IChapterProcessor
{
    /// <summary>
    /// Extracts chapters from a media file and generates chapters.vtt
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="outputDirectory">Directory where chapters.vtt will be written</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with chapter information</returns>
    Task<ChapterExtractionResult> ExtractChaptersAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a media file contains chapters
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if chapters are present</returns>
    Task<bool> HasChaptersAsync(string inputFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets chapter metadata without generating WebVTT file
    /// </summary>
    /// <param name="inputFilePath">Path to the media file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of chapters</returns>
    Task<List<ChapterInfo>> GetChapterMetadataAsync(
        string inputFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates chapter file for the given source.
    /// Alias for ExtractChaptersAsync for compatibility with encoding pipeline.
    /// </summary>
    Task WriteChaptersAsync(
        string sourcePath,
        string outputFolder,
        CancellationToken cancellationToken = default);
}

#region FFprobe Chapter DTOs

/// <summary>
/// Root object for FFprobe chapter JSON output
/// </summary>
internal class FfprobeChapterRoot
{
    [JsonProperty("chapters")] public FfprobeChapter[] Chapters { get; set; } = [];
}

/// <summary>
/// Individual chapter from FFprobe output
/// </summary>
internal class FfprobeChapter
{
    [JsonProperty("id")] public double Id { get; set; }
    [JsonProperty("time_base")] public string TimeBase { get; set; } = string.Empty;
    [JsonProperty("start")] public long Start { get; set; }
    [JsonProperty("start_time")] public double StartTime { get; set; }
    [JsonProperty("end")] public long End { get; set; }
    [JsonProperty("end_time")] public double EndTime { get; set; }
    [JsonProperty("tags")] public FfprobeTags FfprobeTags { get; set; } = new();
}

/// <summary>
/// Tags from FFprobe chapter metadata
/// </summary>
internal class FfprobeTags
{
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
}

#endregion

/// <summary>
/// Extracts chapter markers from media files and generates chapters.vtt for video players.
/// Supports MKV, MP4, and other container formats with embedded chapter metadata.
/// </summary>
public class ChapterProcessor : IChapterProcessor
{
    private const string ChaptersFilename = "chapters.vtt";

    public async Task<ChapterExtractionResult> ExtractChaptersAsync(
        string inputFilePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ChapterExtractionResult result = new()
        {
            OutputPath = Path.Combine(outputDirectory, ChaptersFilename)
        };

        try
        {
            if (!File.Exists(inputFilePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Input file not found: {inputFilePath}";
                return result;
            }

            // Get chapter data from FFprobe
            FfprobeChapterRoot? chapterRoot = await GetFfprobeChaptersAsync(inputFilePath, cancellationToken);

            if (chapterRoot?.Chapters is null || chapterRoot.Chapters.Length == 0)
            {
                // No chapters is a valid state, not an error
                result.Success = true;
                return result;
            }

            // Create output directory if needed
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Convert to ChapterInfo objects
            for (int i = 0; i < chapterRoot.Chapters.Length; i++)
            {
                FfprobeChapter chapter = chapterRoot.Chapters[i];
                result.Chapters.Add(new ChapterInfo
                {
                    Id = i + 1,
                    Title = string.IsNullOrWhiteSpace(chapter.FfprobeTags.Title)
                        ? $"Chapter {i + 1}"
                        : chapter.FfprobeTags.Title,
                    StartTime = chapter.StartTime,
                    EndTime = chapter.EndTime
                });
            }

            // Generate WebVTT file
            await GenerateWebVttAsync(result.OutputPath, result.Chapters, cancellationToken);

            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Chapter extraction was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to extract chapters: {ex.Message}";
        }

        return result;
    }

    public async Task<bool> HasChaptersAsync(string inputFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputFilePath))
        {
            return false;
        }

        FfprobeChapterRoot? chapterRoot = await GetFfprobeChaptersAsync(inputFilePath, cancellationToken);
        return chapterRoot?.Chapters is not null && chapterRoot.Chapters.Length > 0;
    }

    public async Task<List<ChapterInfo>> GetChapterMetadataAsync(
        string inputFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputFilePath))
        {
            return [];
        }

        FfprobeChapterRoot? chapterRoot = await GetFfprobeChaptersAsync(inputFilePath, cancellationToken);

        if (chapterRoot?.Chapters is null || chapterRoot.Chapters.Length == 0)
        {
            return [];
        }

        List<ChapterInfo> chapters = [];
        for (int i = 0; i < chapterRoot.Chapters.Length; i++)
        {
            FfprobeChapter chapter = chapterRoot.Chapters[i];
            chapters.Add(new ChapterInfo
            {
                Id = i + 1,
                Title = string.IsNullOrWhiteSpace(chapter.FfprobeTags.Title)
                    ? $"Chapter {i + 1}"
                    : chapter.FfprobeTags.Title,
                StartTime = chapter.StartTime,
                EndTime = chapter.EndTime
            });
        }

        return chapters;
    }

    /// <summary>
    /// Creates or updates chapter file for the given source.
    /// This method exists for compatibility with encoding pipeline patterns.
    /// </summary>
    public async Task WriteChaptersAsync(
        string sourcePath,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        await ExtractChaptersAsync(sourcePath, outputFolder, cancellationToken);
    }

    private static async Task<FfprobeChapterRoot?> GetFfprobeChaptersAsync(
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string command = BuildFfprobeCommand(inputFilePath);
        string result = await Shell.ExecStdOutAsync(AppFiles.FfProbePath, command);

        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<FfprobeChapterRoot>(result);
    }

    private static string BuildFfprobeCommand(string inputFilePath)
    {
        // Build FFprobe command to extract chapter data as JSON
        // -v quiet: Suppress logging output
        // -print_format json: Output in JSON format
        // -show_chapters: Show chapter information
        return $"-v quiet -print_format json -show_chapters \"{inputFilePath}\"";
    }

    private static async Task GenerateWebVttAsync(
        string outputPath,
        List<ChapterInfo> chapters,
        CancellationToken cancellationToken)
    {
        await using StreamWriter writer = new(outputPath);

        // WebVTT header
        await writer.WriteLineAsync("WEBVTT");
        await writer.WriteLineAsync();

        foreach (ChapterInfo chapter in chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Chapter cue identifier
            await writer.WriteLineAsync($"Chapter {chapter.Id}");

            // Timestamp line: start --> end (format: hh:mm:ss.fff)
            await writer.WriteLineAsync($"{chapter.StartTime.ToHis()} --> {chapter.EndTime.ToHis()}");

            // Chapter title
            await writer.WriteLineAsync(chapter.Title);

            // Empty line to separate cues
            await writer.WriteLineAsync();
        }
    }
}
