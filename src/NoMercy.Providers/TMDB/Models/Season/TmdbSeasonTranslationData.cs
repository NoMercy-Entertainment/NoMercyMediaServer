using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonTranslationData : TmdbSharedTranslationData
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}