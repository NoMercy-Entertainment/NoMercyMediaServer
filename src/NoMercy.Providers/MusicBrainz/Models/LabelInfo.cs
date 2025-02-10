using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class LabelInfo
{
    [JsonProperty("catalog-number")] public string CatalogNumber { get; set; } = string.Empty;
    [JsonProperty("label")] public MusicBrainzLabel MusicBrainzLabel { get; set; } = new();
}