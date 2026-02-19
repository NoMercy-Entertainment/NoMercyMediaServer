using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;

public class AcoustIdFingerprintTrack
{
    [JsonProperty("artists")] public AcoustIdFingerprintArtist[]? Artists { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("position")] public int? Position { get; set; }
}