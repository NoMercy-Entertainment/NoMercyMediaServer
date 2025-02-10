
using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionsTranslations : TmdbSharedTranslations
{
    [JsonProperty("translations")] public new TmdbCollectionsTranslation[] Translations { get; set; } = [];
}
