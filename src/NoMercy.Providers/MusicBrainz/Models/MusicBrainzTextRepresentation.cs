using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzTextRepresentation
{
    [JsonProperty("script")] public string Script { get; set; } = string.Empty;
    [JsonProperty("language")] public string Language { get; set; } = string.Empty;
}