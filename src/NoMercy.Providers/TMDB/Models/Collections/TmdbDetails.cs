using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbDetails
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster_path")] public object? PosterPath { get; set; }
    [JsonProperty("backdrop_path")] public string? BackdropPath { get; set; }
    [JsonProperty("parts")] public TmdbCollectionPart[] Parts { get; set; } = [];
}