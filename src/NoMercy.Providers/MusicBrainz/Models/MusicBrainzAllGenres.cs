using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzAllGenres
{
    [JsonProperty("genres")] public MusicBrainzGenre[] Genres { get; set; } = [];
    [JsonProperty("genre-offset")] public long GenreOffset { get; set; }
    [JsonProperty("genre-count")] public long GenreCount { get; set; }
}