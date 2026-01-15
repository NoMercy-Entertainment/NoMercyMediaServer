using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbSharedTranslations
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("translations")] public TmdbSharedTranslation[] Translations { get; set; } = [];
}