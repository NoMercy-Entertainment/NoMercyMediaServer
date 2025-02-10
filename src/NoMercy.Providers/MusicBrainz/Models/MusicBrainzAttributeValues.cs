using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzAttributeValues
{
    [JsonProperty("task")] public string Task { get; set; } = string.Empty;
}