
using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzTag
{
    [JsonProperty("count")] public int Count { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}
