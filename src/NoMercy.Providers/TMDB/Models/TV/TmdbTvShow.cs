using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvShow : TmdbBase
{
    [JsonProperty("first_air_date")] public DateTime? FirstAirDate { get; set; }
    [JsonProperty("genre_ids")] public int?[] GenreIds { get; set; } = [];

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("origin_country")] public string[] OriginCountry { get; set; } = [];

    [JsonProperty("original_name")] public string OriginalName { get; set; } = string.Empty;
    [JsonProperty("type")] public string MediaType { get; set; } = string.Empty;
}