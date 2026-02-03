using NoMercy.Encoder;

namespace NoMercy.EncoderV2.Validation;

/// <summary>
/// Validates encoding output files
/// </summary>
public interface IOutputValidator
{
    Task<OutputValidationResult> ValidateOutputAsync(string filePath, TimeSpan expectedDuration);
    Task<OutputValidationResult> ValidatePlaylistAsync(string playlistPath);
}

public class OutputValidationResult
{
    public bool IsValid { get; set; }
    public bool FileExists { get; set; }
    public long FileSizeBytes { get; set; }
    public TimeSpan? ActualDuration { get; set; }
    public TimeSpan? ExpectedDuration { get; set; }
    public double DurationDifferenceSeconds { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class OutputValidator : IOutputValidator
{
    public async Task<OutputValidationResult> ValidateOutputAsync(string filePath, TimeSpan expectedDuration)
    {
        OutputValidationResult result = new()
        {
            ExpectedDuration = expectedDuration
        };

        if (!File.Exists(filePath))
        {
            result.FileExists = false;
            result.IsValid = false;
            result.Errors.Add($"Output file does not exist: {filePath}");
            return result;
        }

        result.FileExists = true;

        FileInfo fileInfo = new(filePath);
        result.FileSizeBytes = fileInfo.Length;

        if (result.FileSizeBytes == 0)
        {
            result.IsValid = false;
            result.Errors.Add("Output file is empty (0 bytes)");
            return result;
        }

        if (result.FileSizeBytes < 1024)
        {
            result.Warnings.Add($"Output file is very small ({result.FileSizeBytes} bytes)");
        }

        try
        {
            Ffprobe ffprobe = new(filePath);
            await ffprobe.GetStreamData();

            result.ActualDuration = ffprobe.Format.Duration ?? TimeSpan.Zero;

            if (result.ActualDuration == TimeSpan.Zero)
            {
                result.Errors.Add("Could not determine output file duration");
                result.IsValid = false;
                return result;
            }

            result.DurationDifferenceSeconds = Math.Abs((result.ActualDuration.Value - expectedDuration).TotalSeconds);

            if (result.DurationDifferenceSeconds > 2.0)
            {
                result.Warnings.Add(
                    $"Duration mismatch: expected {expectedDuration:hh\\:mm\\:ss}, actual {result.ActualDuration:hh\\:mm\\:ss} (diff: {result.DurationDifferenceSeconds:F2}s)"
                );
            }

            if (ffprobe.VideoStreams.Count == 0 && ffprobe.AudioStreams.Count == 0)
            {
                result.Errors.Add("Output file contains no video or audio streams");
                result.IsValid = false;
                return result;
            }

            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to validate output file: {ex.Message}");
        }

        return result;
    }

    public async Task<OutputValidationResult> ValidatePlaylistAsync(string playlistPath)
    {
        OutputValidationResult result = new();

        if (!File.Exists(playlistPath))
        {
            result.FileExists = false;
            result.IsValid = false;
            result.Errors.Add($"Playlist file does not exist: {playlistPath}");
            return result;
        }

        result.FileExists = true;

        FileInfo fileInfo = new(playlistPath);
        result.FileSizeBytes = fileInfo.Length;

        if (result.FileSizeBytes == 0)
        {
            result.IsValid = false;
            result.Errors.Add("Playlist file is empty (0 bytes)");
            return result;
        }

        try
        {
            string content = await File.ReadAllTextAsync(playlistPath);
            string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (!content.StartsWith("#EXTM3U"))
            {
                result.Errors.Add("Playlist does not start with #EXTM3U header");
                result.IsValid = false;
                return result;
            }

            int segmentCount = lines.Count(l => l.StartsWith("#EXTINF:"));
            int fileCount = lines.Count(l => !l.StartsWith("#") && l.Trim().Length > 0);

            if (segmentCount == 0)
            {
                result.Errors.Add("Playlist contains no segments");
                result.IsValid = false;
                return result;
            }

            if (fileCount != segmentCount)
            {
                result.Warnings.Add($"Segment count mismatch: {segmentCount} #EXTINF tags but {fileCount} file references");
            }

            string? directory = Path.GetDirectoryName(playlistPath);
            if (directory != null)
            {
                foreach (string line in lines)
                {
                    if (!line.StartsWith("#") && line.Trim().Length > 0 && !line.StartsWith("http"))
                    {
                        string segmentPath = Path.Combine(directory, line.Trim());
                        if (!File.Exists(segmentPath))
                        {
                            result.Warnings.Add($"Referenced segment file not found: {line.Trim()}");
                        }
                    }
                }
            }

            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to validate playlist: {ex.Message}");
        }

        return result;
    }
}
