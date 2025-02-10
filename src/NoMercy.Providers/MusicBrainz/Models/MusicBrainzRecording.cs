
using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzRecording
{
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("video")] public bool Video { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("length")] public int? Length { get; set; }
    [JsonProperty("genres")] public MusicBrainzGenreDetails[] Genres { get; set; } = [];
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
}
