using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class RelationRecording
{
    [JsonProperty("artist-credit")] public RecordingArtistCredit[] ArtistCredit { get; set; } = [];
    [JsonProperty("disambiguation")] public string Disambiguation { get; set; } = string.Empty;

    [JsonProperty("id")] public Guid Id { get; set; }

    // [JsonProperty("isrcs")] public object[] Isrcs { get; set; }
    [JsonProperty("length")] public int? Length { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("video")] public bool Video { get; set; }
}