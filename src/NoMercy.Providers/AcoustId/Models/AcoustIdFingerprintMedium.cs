using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;
public class AcoustIdFingerprintMedium
{
    [JsonProperty("format")] public string? Format { get; set; }
    [JsonProperty("position")] public int? Position { get; set; }
    [JsonProperty("track_count")] public int? TrackCount { get; set; }
    [JsonProperty("tracks")] public AcoustIdFingerprintTrack[] Tracks { get; set; } = [];
}