namespace NoMercy.Encoder.Output;

/// <summary>
/// Resolves naming templates used in encoder profiles. Both syntaxes are
/// supported: legacy <c>:token:</c> and modern <c>{token}</c>. The V2 built-in
/// presets use the brace form (<c>video/{label}</c>, <c>audio/{lang}-{codec}</c>);
/// earlier V1 rows persisted with colons (<c>:type:_:framesize:_:colorrange:</c>).
///
/// Tokens:
///   type      → "video" or "audio"
///   framesize → "1920x1080"
///   label     → alias of framesize (V2 brace presets)
///   colorrange → "HDR" or "SDR"
///   language  → "eng", "und"
///   lang      → alias of language (V2 brace presets)
///   codec     → "aac", "eac3", "opus"
///   channels  → "2", "6"
///   filename  → source filename without extension
///   variant   → "full", "sign", "song" (subtitle type)
/// </summary>
public static class TemplateResolver
{
    public static string Resolve(string template, Dictionary<string, string> values)
    {
        string result = template;
        foreach (KeyValuePair<string, string> kvp in values)
        {
            result = result.Replace($":{kvp.Key}:", kvp.Value);
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
        }

        return result;
    }

    public static Dictionary<string, string> VideoTokens(int width, int height, bool isHdrOutput)
    {
        string framesize = $"{width}x{height}";
        return new()
        {
            ["type"] = "video",
            ["framesize"] = framesize,
            // {label} is the V2 brace-style alias for framesize — every V2 builtin
            // emits "video/{label}" expecting the resolution string.
            ["label"] = framesize,
            // HDR labelling derives from the actual transfer pipeline that the
            // OutputPlan builder set, never from bit depth. 10-bit BT.709 is
            // SDR; an 8-bit HDR stream (rare but possible) would still be HDR.
            ["colorrange"] = isHdrOutput ? "HDR" : "SDR",
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
            // {lang} is the V2 brace-style alias for language.
            ["lang"] = language,
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
            ["lang"] = language,
            ["variant"] = variant,
            ["filename"] = filename,
        };
    }
}
