using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvTranslations : TmdbSharedTranslations
{
    [JsonProperty("translations")] public new TmdbTvTranslation[] Translations { get; set; } = [];
}