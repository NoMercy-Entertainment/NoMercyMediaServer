using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionsTranslationData : TmdbSharedTranslationData
{
    [JsonProperty("title")] public string? Title { get; set; }
}