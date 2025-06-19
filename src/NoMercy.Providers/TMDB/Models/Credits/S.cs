using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Credits;

public class S : Season.TmdbSeason
{
    [JsonProperty("air_date")] public new DateTime AirDate { get; set; }
    [JsonProperty("poster_path")] public new string? PosterPath { get; set; }
    [JsonProperty("season_number")] public new int SeasonNumber { get; set; }
}