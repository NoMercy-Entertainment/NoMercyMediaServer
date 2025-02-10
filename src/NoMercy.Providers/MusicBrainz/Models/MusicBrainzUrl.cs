using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzUrl
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("resource")] public Uri Resource { get; set; } = null!;
}