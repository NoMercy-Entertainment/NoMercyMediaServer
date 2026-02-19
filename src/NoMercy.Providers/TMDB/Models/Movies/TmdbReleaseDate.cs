using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbReleaseDate
{
    [JsonProperty("certification")] public string Certification { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;
    [JsonProperty("release_date")] public DateTime ReleaseDateReleaseDate { get; set; } = DateTime.MinValue;

    [JsonProperty("type")] public int Type { get; set; }
    [JsonProperty("note")] public string? Note { get; set; }
}