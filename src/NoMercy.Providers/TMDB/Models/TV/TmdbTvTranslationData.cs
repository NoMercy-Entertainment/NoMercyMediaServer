using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvTranslationData : TmdbSharedTranslationData
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}