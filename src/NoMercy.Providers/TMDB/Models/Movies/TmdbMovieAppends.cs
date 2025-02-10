
using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Combined;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieAppends : TmdbMovieDetails
{
    [JsonProperty("alternative_titles")] public TmdbMovieAlternativeTitles AlternativeTitles { get; set; } = new();
    [JsonProperty("credits")] public TmdbMovieCredits Credits { get; set; } = new();
    [JsonProperty("external_ids")] public TmdbMovieExternalIds ExternalIds { get; set; } = new();
    [JsonProperty("images")] public TmdbImages Images { get; set; } = new();
    [JsonProperty("keywords")] public TmdbMovieKeywords Keywords { get; set; } = new();
    [JsonProperty("recommendations")] public TmdbMovieRecommendations Recommendations { get; set; } = new();
    [JsonProperty("similar")] public TmdbMovieSimilar Similar { get; set; } = new();
    [JsonProperty("translations")] public TmdbCombinedTranslations Translations { get; set; } = new();
    [JsonProperty("videos")] public TmdbMovieVideos Videos { get; set; } = new();
    [JsonProperty("watch/providers")] public TmdbMovieWatchProviders WatchProviders { get; set; } = new();
    [JsonProperty("genres")] public new TmdbGenre[] Genres { get; set; } = [];
    [JsonProperty("release_dates")] public TmdbMovieReleaseDates ReleaseDates { get; set; } = new();
}
