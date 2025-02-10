using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Combined;


namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionAppends : TmdbCollectionDetails
{
    [JsonProperty("images")] public TmdbCollectionImages Images { get; set; } = new();
    [JsonProperty("translations")] public TmdbCombinedTranslations Translations { get; set; } = new();
}
