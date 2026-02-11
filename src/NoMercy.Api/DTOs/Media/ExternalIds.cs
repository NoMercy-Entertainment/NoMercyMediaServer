using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record ExternalIds
{
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("tvdb_id")] public int? TvdbId { get; set; }
}