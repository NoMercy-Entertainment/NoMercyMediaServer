using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Combined;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeAppends : TmdbEpisodeDetails
{
    [JsonProperty("credits")] public TmdbEpisodeCredits TmdbEpisodeCredits { get; set; } = new();
    [JsonProperty("changes")] public TmdbEpisodeChanges Changes { get; set; } = new();
    [JsonProperty("external_ids")] public TmdbEpisodeExternalIds TmdbEpisodeExternalIds { get; set; } = new();
    [JsonProperty("images")] public TmdbEpisodeImages TmdbEpisodeImages { get; set; } = new();
    [JsonProperty("translations")] public TmdbCombinedTranslations Translations { get; set; } = new();
    [JsonProperty("videos")] public Videos Videos { get; set; } = new();
}