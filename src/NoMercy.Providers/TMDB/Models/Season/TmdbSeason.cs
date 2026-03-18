using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeason
{
    [JsonProperty("air_date")] public DateTime? AirDate { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string? Name { get; set; } = string.Empty;
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster_path")] public string? PosterPath { get; set; }
    [JsonProperty("season_number")] public int SeasonNumber { get; set; }
}