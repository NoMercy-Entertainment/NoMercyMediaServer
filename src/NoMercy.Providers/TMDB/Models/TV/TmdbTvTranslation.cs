using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvTranslation : TmdbSharedTranslation
{
    [JsonProperty("data")] public new TmdbTvTranslationData Data { get; set; } = new();
}