
using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzGenre
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("disambiguation")] public string Disambiguation { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}
