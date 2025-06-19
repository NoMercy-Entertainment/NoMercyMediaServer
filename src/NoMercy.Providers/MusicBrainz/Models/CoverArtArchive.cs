using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;

public class CoverArtArchive
{
    [JsonProperty("artwork")] public bool Artwork { get; set; }
    [JsonProperty("back")] public bool Back { get; set; }
    [JsonProperty("count")] public int Count { get; set; }
    [JsonProperty("darkened")] public bool Darkened { get; set; }
    [JsonProperty("front")] public bool Front { get; set; }
}