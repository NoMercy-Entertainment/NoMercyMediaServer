using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollection
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("poster_path")] public string PosterPath { get; set; } = string.Empty;
    [JsonProperty("backdrop_path")] public string BackdropPath { get; set; } = string.Empty;
}
