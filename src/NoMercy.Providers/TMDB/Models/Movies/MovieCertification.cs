using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class MovieCertification
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("rating")] public string Rating { get; set; } = string.Empty;
    [JsonProperty("descriptors")] public string[] Descriptors { get; set; } = [];
}