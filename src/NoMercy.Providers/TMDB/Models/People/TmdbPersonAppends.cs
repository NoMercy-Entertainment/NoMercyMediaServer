using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonAppends : TmdbPersonDetails
{
    [JsonProperty("movie_credits")] public TmdbPersonCredits MovieCredits { get; set; } = new();
    [JsonProperty("credits")] public TmdbPersonCredits Credits { get; set; } = new();
    [JsonProperty("combined_credits")] public TmdbPersonCredits CombinedCredits { get; set; } = new();
    [JsonProperty("tv_credits")] public TmdbPersonCredits TvCredits { get; set; } = new();
    [JsonProperty("images")] public TmdbPersonImages Images { get; set; } = new();
    [JsonProperty("translations")] public TmdbPersonTranslations Translations { get; set; } = new();
}