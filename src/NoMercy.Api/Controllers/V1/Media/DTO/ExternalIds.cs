using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record ExternalIds
{
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("tvdb_id")] public int? TvdbId { get; set; }
}