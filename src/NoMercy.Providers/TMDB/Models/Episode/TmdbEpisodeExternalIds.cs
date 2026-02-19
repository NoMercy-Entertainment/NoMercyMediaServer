using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeExternalIds
{
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("freebase_mid")] public string? FreebaseMid { get; set; }
    [JsonProperty("freebase_id")] public string? FreebaseId { get; set; }
    [JsonProperty("tvrage_id")] public int? TvRageId { get; set; }
    [JsonProperty("tvdb_id")] public int? TvdbId { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
}