using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class TrackSubtitlesGetMessageBody
{
    [JsonProperty("subtitle_list")] public SubtitleList[] SubtitleList { get; set; } = [];
}