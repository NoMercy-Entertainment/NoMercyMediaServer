using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class ReleaseArtistCredit
{
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("joinphrase")] public string Joinphrase { get; set; } = string.Empty;
    [JsonProperty("artist")] public MusicBrainzArtistDetails MusicBrainzArtist { get; set; } = new();
}