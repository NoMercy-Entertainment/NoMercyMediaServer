using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class SubtitleList
{
    [JsonProperty("subtitle")] public MusixMatchSubtitle? Subtitle { get; set; }
}