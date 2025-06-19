using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonTranslation : TmdbSharedTranslation
{
    [JsonProperty("data")] public new TmdbSeasonTranslationData Data { get; set; } = new();
}