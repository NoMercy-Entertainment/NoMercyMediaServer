using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchLyrics
{
    [JsonProperty("lyrics_id")] public long LyricsId { get; set; }
    [JsonProperty("can_edit")] public long CanEdit { get; set; }

    [JsonProperty("check_validation_overridable")]
    public long CheckValidationOverridable { get; set; }

    [JsonProperty("locked")] public long Locked { get; set; }
    [JsonProperty("published_status")] public long PublishedStatus { get; set; }
    [JsonProperty("action_requested")] public string ActionRequested { get; set; } = string.Empty;
    [JsonProperty("verified")] public long Verified { get; set; }
    [JsonProperty("restricted")] public long Restricted { get; set; }
    [JsonProperty("instrumental")] public long Instrumental { get; set; }
    [JsonProperty("explicit")] public long Explicit { get; set; }
    [JsonProperty("lyrics_body")] public string LyricsBody { get; set; } = string.Empty;
    [JsonProperty("lyrics_language")] public string LyricsLanguage { get; set; } = string.Empty;

    [JsonProperty("lyrics_language_description")]
    public string LyricsLanguageDescription { get; set; } = string.Empty;

    [JsonProperty("script_tracking_url")] public Uri ScriptTrackingUrl { get; set; } = null!;
    [JsonProperty("pixel_tracking_url")] public Uri PixelTrackingUrl { get; set; } = null!;
    [JsonProperty("html_tracking_url")] public Uri HtmlTrackingUrl { get; set; } = null!;
    [JsonProperty("lyrics_copyright")] public string LyricsCopyright { get; set; } = string.Empty;
    [JsonProperty("writer_list")] public object[] WriterList { get; set; } = [];
    [JsonProperty("publisher_list")] public object[] PublisherList { get; set; } = [];
    [JsonProperty("backlink_url")] public Uri BacklinkUrl { get; set; } = null!;
    [JsonProperty("updated_time")] public DateTimeOffset UpdatedTime { get; set; }
}
