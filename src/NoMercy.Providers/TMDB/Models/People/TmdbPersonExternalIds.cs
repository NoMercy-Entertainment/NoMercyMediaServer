using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonExternalIds
{
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("facebook_id")] public string? FacebookId { get; set; }
    [JsonProperty("freebase_mid")] public string? FreebaseMid { get; set; }
    [JsonProperty("freebase_id")] public string? FreebaseId { get; set; }
    [JsonProperty("twitter_id")] public string? TwitterId { get; set; }
    [JsonProperty("tvrage_id")] public string? TvRageId { get; set; }
    [JsonProperty("wikidata_id")] public string? WikipediaId { get; set; }
    [JsonProperty("instagram_id")] public string? InstagramId { get; set; }
    [JsonProperty("tiktok_id")] public string? TikTokId { get; set; }
    [JsonProperty("youtube_id")] public string? YoutubeId { get; set; }
}