using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;


namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchSubtitle
{
    [JsonProperty("subtitle_id")] public long SubtitleId { get; set; }
    [JsonProperty("restricted")] public long Restricted { get; set; }
    [JsonProperty("published_status")] public long PublishedStatus { get; set; }

    [Column("SubtitleBody")]
    [JsonProperty("subtitle_body")]
    [System.Text.Json.Serialization.JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _subtitle_body { get; set; }

    [NotMapped]
    public MusixMatchFormattedLyric[]? SubtitleBody
    {
        get => _subtitle_body is null
            ? null
            : JsonConvert.DeserializeObject<MusixMatchFormattedLyric[]>(_subtitle_body);
        set => _subtitle_body = JsonConvert.SerializeObject(value);
    }

    [JsonProperty("subtitle_avg_count")] public long SubtitleAvgCount { get; set; }
    [JsonProperty("lyrics_copyright")] public string LyricsCopyright { get; set; } = string.Empty;
    [JsonProperty("subtitle_length")] public long SubtitleLength { get; set; }
    [JsonProperty("subtitle_language")] public string SubtitleLanguage { get; set; } = string.Empty;

    [JsonProperty("subtitle_language_description")] public string SubtitleLanguageDescription { get; set; } = string.Empty;

    [JsonProperty("script_tracking_url")] public Uri ScriptTrackingUrl { get; set; } = null!;
    [JsonProperty("pixel_tracking_url")] public Uri PixelTrackingUrl { get; set; } = null!;
    [JsonProperty("html_tracking_url")] public Uri HtmlTrackingUrl { get; set; } = null!;
    [JsonProperty("writer_list")] public object[] WriterList { get; set; } = [];
    [JsonProperty("publisher_list")] public object[] PublisherList { get; set; } = [];
    [JsonProperty("updated_time")] public DateTimeOffset UpdatedTime { get; set; }
}
