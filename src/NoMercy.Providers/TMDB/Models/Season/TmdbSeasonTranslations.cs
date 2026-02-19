using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonTranslations : TmdbSharedTranslations
{
    [JsonProperty("translations")] public new TmdbSeasonTranslation[] Translations { get; set; } = [];
}