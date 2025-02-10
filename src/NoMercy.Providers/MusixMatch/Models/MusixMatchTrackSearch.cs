using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackSearch
{
    [JsonProperty("track_list")] public List<MusixMatchTrackList> Results { get; set; } = [];
}
