using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzGenreDetails : MusicBrainzGenre
{
    [JsonProperty("count")] public long Count { get; set; }
}