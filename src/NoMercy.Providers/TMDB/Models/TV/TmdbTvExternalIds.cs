using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvExternalIds
{
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("freebase_mid")] public string? FreebaseMid { get; set; }
    [JsonProperty("freebase_id")] public string? FreebaseId { get; set; }
    [JsonProperty("tvdb_id")] public int? TvdbId { get; set; }
    [JsonProperty("tvrage_id")] public int? TvrageId { get; set; }
    [JsonProperty("facebook_id")] public string? FacebookId { get; set; }
    [JsonProperty("instagram_id")] public string? InstagramId { get; set; }
    [JsonProperty("twitter_id")] public string? TwitterId { get; set; }
}