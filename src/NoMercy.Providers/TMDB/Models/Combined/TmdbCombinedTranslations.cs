using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Combined;

public class TmdbCombinedTranslations
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("translations")] public TmdbCombinedTranslation[] Translations { get; set; } = [];
}