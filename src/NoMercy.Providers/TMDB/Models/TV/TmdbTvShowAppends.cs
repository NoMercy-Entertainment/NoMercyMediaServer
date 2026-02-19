using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Combined;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvShowAppends : TmdbTvShowDetails
{
    [JsonProperty("aggregate_credits")] public TmdbTvAggregatedCredits AggregateCredits { get; set; } = new();
    [JsonProperty("alternative_titles")] public TmdbTvAlternativeTitles AlternativeTitles { get; set; } = new();
    [JsonProperty("content_ratings")] public TmdbTvContentRatings ContentRatings { get; set; } = new();
    [JsonProperty("credits")] public TmdbTvCredits Credits { get; set; } = new();
    [JsonProperty("external_ids")] public TmdbTvExternalIds ExternalIds { get; set; } = new();
    [JsonProperty("images")] public TmdbImages Images { get; set; } = new();
    [JsonProperty("keywords")] public TmdbTvKeywords Keywords { get; set; } = new();
    [JsonProperty("recommendations")] public TmdbTvRecommendations Recommendations { get; set; } = new();
    [JsonProperty("similar")] public TmdbTvSimilar Similar { get; set; } = new();
    [JsonProperty("translations")] public TmdbCombinedTranslations Translations { get; set; } = new();
    [JsonProperty("videos")] public TmdbTvVideos Videos { get; set; } = new();
    [JsonProperty("watch/providers")] public TmdbWatchProviders WatchProviders { get; set; } = new();
}