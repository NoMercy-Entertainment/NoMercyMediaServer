using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class EpisodeTranslation : TmdbSharedTranslation
{
    [JsonProperty("data")] public new EpisodeTranslationData Data { get; set; } = new();
}