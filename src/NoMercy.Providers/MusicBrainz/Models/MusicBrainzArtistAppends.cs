using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzArtistAppends : MusicBrainzArtistDetails
{
    [JsonProperty("gender")] public string Gender { get; set; } = string.Empty;
    [JsonProperty("recordings")] public MusicBrainzRecording[] Recordings { get; set; } = [];
}