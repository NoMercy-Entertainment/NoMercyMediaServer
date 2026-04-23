namespace NoMercy.Encoder.Output;

/// <summary>
/// Resolves naming templates used in encoder profiles.
/// Templates use :token: syntax that gets replaced with actual values.
///
/// Supported tokens:
///   :type:      → "video" or "audio"
///   :framesize: → "1920x1080"
///   :language:  → "eng", "und"
///   :codec:     → "aac", "eac3", "opus"
///   :filename:  → source filename without extension
///   :variant:   → "full", "sign", "song" (subtitle type)
/// </summary>
public static class TemplateResolver
{
    public static string Resolve(string template, Dictionary<string, string> values)
    {
        string result = template;
        foreach (KeyValuePair<string, string> kvp in values)
        {
            result = result.Replace($":{kvp.Key}:", kvp.Value);
        }

        return result;
    }

    public static Dictionary<string, string> VideoTokens(int width, int height, bool tenBit)
    {
        return new()
        {
            ["type"] = "video",
            ["framesize"] = $"{width}x{height}",
            ["colorrange"] = tenBit ? "HDR" : "SDR",
        };
    }

    public static Dictionary<string, string> AudioTokens(
        string language,
        string codecName,
        int channels
    )
    {
        return new()
        {
            ["type"] = "audio",
            ["language"] = language,
            ["codec"] = codecName,
            ["channels"] = channels.ToString(),
        };
    }

    public static Dictionary<string, string> SubtitleTokens(
        string language,
        string variant,
        string filename
    )
    {
        return new()
        {
            ["language"] = language,
            ["variant"] = variant,
            ["filename"] = filename,
        };
    }
}
