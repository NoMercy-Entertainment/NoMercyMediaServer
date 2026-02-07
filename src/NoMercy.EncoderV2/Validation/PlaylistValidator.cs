using System.Globalization;

namespace NoMercy.EncoderV2.Validation;

/// <summary>
/// Validates HLS playlist files and manifests
/// </summary>
public interface IPlaylistValidator
{
    Task<PlaylistValidationResult> ValidateMasterPlaylistAsync(string playlistPath);
    Task<PlaylistValidationResult> ValidateMediaPlaylistAsync(string playlistPath);
}

public class PlaylistValidationResult
{
    public bool IsValid { get; set; }
    public string PlaylistType { get; set; } = string.Empty;
    public int VariantCount { get; set; }
    public int SegmentCount { get; set; }
    public double TotalDurationSeconds { get; set; }
    public double TargetDuration { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<PlaylistVariant> Variants { get; set; } = [];
}

public class PlaylistVariant
{
    public int Bandwidth { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string Codecs { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
}

public class PlaylistValidator : IPlaylistValidator
{
    public async Task<PlaylistValidationResult> ValidateMasterPlaylistAsync(string playlistPath)
    {
        PlaylistValidationResult result = new()
        {
            PlaylistType = "master"
        };

        if (!File.Exists(playlistPath))
        {
            result.IsValid = false;
            result.Errors.Add($"Master playlist file not found: {playlistPath}");
            return result;
        }

        try
        {
            string content = await File.ReadAllTextAsync(playlistPath);
            string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (!content.StartsWith("#EXTM3U"))
            {
                result.Errors.Add("Master playlist does not start with #EXTM3U");
                result.IsValid = false;
                return result;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.StartsWith("#EXT-X-STREAM-INF:"))
                {
                    PlaylistVariant variant = ParseVariant(line);

                    if (i + 1 < lines.Length)
                    {
                        variant.Uri = lines[i + 1].Trim();
                    }

                    result.Variants.Add(variant);
                }
            }

            result.VariantCount = result.Variants.Count;

            if (result.VariantCount == 0)
            {
                result.Errors.Add("Master playlist contains no variants");
                result.IsValid = false;
                return result;
            }

            string? directory = Path.GetDirectoryName(playlistPath);
            if (directory != null)
            {
                foreach (PlaylistVariant variant in result.Variants)
                {
                    if (!variant.Uri.StartsWith("http"))
                    {
                        string variantPath = Path.Combine(directory, variant.Uri);
                        if (!File.Exists(variantPath))
                        {
                            result.Warnings.Add($"Variant playlist not found: {variant.Uri}");
                        }
                    }
                }
            }

            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to validate master playlist: {ex.Message}");
        }

        return result;
    }

    public async Task<PlaylistValidationResult> ValidateMediaPlaylistAsync(string playlistPath)
    {
        PlaylistValidationResult result = new()
        {
            PlaylistType = "media"
        };

        if (!File.Exists(playlistPath))
        {
            result.IsValid = false;
            result.Errors.Add($"Media playlist file not found: {playlistPath}");
            return result;
        }

        try
        {
            string content = await File.ReadAllTextAsync(playlistPath);
            string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (!content.StartsWith("#EXTM3U"))
            {
                result.Errors.Add("Media playlist does not start with #EXTM3U");
                result.IsValid = false;
                return result;
            }

            double totalDuration = 0;
            int segmentCount = 0;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#EXT-X-TARGETDURATION:"))
                {
                    string durationStr = trimmed.Substring("#EXT-X-TARGETDURATION:".Length);
                    if (double.TryParse(durationStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetDuration))
                    {
                        result.TargetDuration = targetDuration;
                    }
                }
                else if (trimmed.StartsWith("#EXTINF:"))
                {
                    segmentCount++;
                    string durationStr = trimmed.Substring("#EXTINF:".Length);
                    int commaIndex = durationStr.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        durationStr = durationStr.Substring(0, commaIndex);
                    }

                    if (double.TryParse(durationStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double segmentDuration))
                    {
                        totalDuration += segmentDuration;

                        if (result.TargetDuration > 0 && segmentDuration > result.TargetDuration)
                        {
                            result.Warnings.Add($"Segment duration ({segmentDuration}s) exceeds target duration ({result.TargetDuration}s)");
                        }
                    }
                }
            }

            result.SegmentCount = segmentCount;
            result.TotalDurationSeconds = totalDuration;

            if (result.SegmentCount == 0)
            {
                result.Errors.Add("Media playlist contains no segments");
                result.IsValid = false;
                return result;
            }

            if (result.TargetDuration == 0)
            {
                result.Warnings.Add("Media playlist missing #EXT-X-TARGETDURATION tag");
            }

            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to validate media playlist: {ex.Message}");
        }

        return result;
    }

    private static PlaylistVariant ParseVariant(string streamInfLine)
    {
        PlaylistVariant variant = new();

        string[] parts = streamInfLine.Substring("#EXT-X-STREAM-INF:".Length).Split(',');

        foreach (string part in parts)
        {
            string[] keyValue = part.Split('=');
            if (keyValue.Length != 2)
            {
                continue;
            }

            string key = keyValue[0].Trim();
            string value = keyValue[1].Trim().Trim('"');

            switch (key)
            {
                case "BANDWIDTH":
                    if (int.TryParse(value, out int bandwidth))
                    {
                        variant.Bandwidth = bandwidth;
                    }
                    break;
                case "RESOLUTION":
                    variant.Resolution = value;
                    break;
                case "CODECS":
                    variant.Codecs = value;
                    break;
            }
        }

        return variant;
    }
}
