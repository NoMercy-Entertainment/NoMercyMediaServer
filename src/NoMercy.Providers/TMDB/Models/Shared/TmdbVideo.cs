using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbVideo
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string? Iso6391 { get; set; }
    [JsonProperty("iso_3166_1")] public string? Iso31661 { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("site")] public string Site { get; set; } = string.Empty;
    [JsonProperty("size")] public int Size { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("official")] public bool Official { get; set; }
    [JsonProperty("published_at")] public DateTime? PublishedAt { get; set; }
}