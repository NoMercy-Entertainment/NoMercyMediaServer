using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Combined;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonAppends : TmdbSeasonDetails
{
    [JsonProperty("aggregate_credits")] public TmdbSeasonAggregatedCredits AggregateCredits { get; set; } = new();

    [JsonProperty("changes")] public TmdbSeasonChanges? Changes { get; set; }
    [JsonProperty("credits")] public TmdbSeasonCredits TmdbSeasonCredits { get; set; } = new();
    [JsonProperty("external_ids")] public TmdbSeasonExternalIds TmdbSeasonExternalIds { get; set; } = new();
    [JsonProperty("images")] public TmdbSeasonImages TmdbSeasonImages { get; set; } = new();
    [JsonProperty("translations")] public TmdbCombinedTranslations Translations { get; set; } = new();
    [JsonProperty("videos")] public TmdbSeasonVideos TmdbSeasonVideos { get; set; } = new();
}