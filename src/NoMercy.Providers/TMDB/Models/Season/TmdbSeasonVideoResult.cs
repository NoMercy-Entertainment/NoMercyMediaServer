using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonVideoResult
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("site")] public Uri? Site { get; set; }
    [JsonProperty("size")] public int Size { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("official")] public bool Official { get; set; }
    [JsonProperty("published_at")] public DateTime? PublishedAt { get; set; }
}