using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchSnippet
{
    [JsonProperty("snippet_id")] public long SnippetId { get; set; }
    [JsonProperty("snippet_language")] public string SnippetLanguage { get; set; } = string.Empty;
    [JsonProperty("restricted")] public long Restricted { get; set; }
    [JsonProperty("instrumental")] public long Instrumental { get; set; }
    [JsonProperty("snippet_body")] public string SnippetBody { get; set; } = string.Empty;
    [JsonProperty("script_tracking_url")] public Uri ScriptTrackingUrl { get; set; } = null!;
    [JsonProperty("pixel_tracking_url")] public Uri PixelTrackingUrl { get; set; } = null!;
    [JsonProperty("html_tracking_url")] public Uri HtmlTrackingUrl { get; set; } = null!;
    [JsonProperty("updated_time")] public DateTimeOffset UpdatedTime { get; set; }
}
