using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class RecordingArtistCredit
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("artist")] public PurpleArtist Artist { get; set; } = new();
    [JsonProperty("joinphrase")] public string Joinphrase { get; set; } = string.Empty;
}