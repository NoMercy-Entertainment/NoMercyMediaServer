using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class EpisodeTranslationData : TmdbSharedTranslationData
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}