using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;
public class AcoustIdFingerprintArtist
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("joinphrase")] public string Joinphrase { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}