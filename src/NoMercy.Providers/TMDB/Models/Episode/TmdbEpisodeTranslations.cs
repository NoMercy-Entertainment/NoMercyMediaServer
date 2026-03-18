using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeTranslations : TmdbSharedTranslations
{
    [JsonProperty("translations")] public new EpisodeTranslation[] Translations { get; set; } = [];
}