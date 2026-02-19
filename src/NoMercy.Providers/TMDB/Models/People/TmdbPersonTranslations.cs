using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonTranslations
{
    [JsonProperty("translations")] public TmdbPersonTranslation[] Translations { get; set; } = [];
}