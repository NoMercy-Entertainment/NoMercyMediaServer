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
        // {label} produces the per-variant folder name that matches the example
        // media layout: HDR variants stay bare ("1920x795"), SDR tonemaps get
        // an explicit "_SDR" suffix ("1920x795_SDR"). Single token so builtins
        // can use one template ("video_{label}/video_{label}") for both.
        string label = isHdrOutput ? framesize : $"{framesize}_SDR";
        return new()
        {
            ["type"] = "video",
            ["framesize"] = framesize,
            ["label"] = label,
            ["colorspace"] = isHdrOutput ? "HDR" : "SDR",
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
            // {type} alias for subtitle templates — the spec uses
            // "subtitles/{filename}.{lang}.{type}" where {type} is the variant
            // (full / sign / song / forced). Distinct from the global "type"
            // token in video/audio contexts where it resolves to "video"/"audio".
            ["type"] = variant,
            ["filename"] = filename,
        };
    }
}
