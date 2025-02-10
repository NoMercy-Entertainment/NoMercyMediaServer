using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class Disc
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("offset-count")] public int OffsetCount { get; set; }
    [JsonProperty("offsets")] public int[] Offsets { get; set; } = [];
    [JsonProperty("sectors")] public int Sectors { get; set; }
}