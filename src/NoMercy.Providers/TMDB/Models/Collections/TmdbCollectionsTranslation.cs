using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Collections;
public class TmdbCollectionsTranslation : TmdbSharedTranslation
{
    [JsonProperty("data")] public new TmdbCollectionsTranslationData Data { get; set; } = new();
}