using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchRichSync
{
    [JsonProperty("richsync_id")] public int RichsyncId;
    [JsonProperty("restricted")] public int Restricted;
    [JsonProperty("richsync_body")] public string RichsyncBody = string.Empty;
    [JsonProperty("lyrics_copyright")] public string LyricsCopyright = string.Empty;
    [JsonProperty("richsync_length")] public int RichsyncLength;
    [JsonProperty("richsync_language")] public string RichsyncLanguage = string.Empty;

    [JsonProperty("richsync_language_description")] public string RichsyncLanguageDescription = string.Empty;

    [JsonProperty("script_tracking_url")] public string ScriptTrackingUrl = string.Empty;
    [JsonProperty("updated_time")] public DateTime UpdatedTime;
}
