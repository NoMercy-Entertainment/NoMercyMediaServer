using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzArtistCredit
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("joinphrase")] public string Joinphrase { get; set; } = string.Empty;
    [JsonProperty("artist")] public MusicBrainzArtist MusicBrainzArtist { get; set; } = new();
}