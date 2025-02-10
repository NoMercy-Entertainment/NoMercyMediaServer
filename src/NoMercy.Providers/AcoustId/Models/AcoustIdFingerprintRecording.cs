using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;
public class AcoustIdFingerprintRecording
{
    [JsonProperty("artists")] public AcoustIdFingerprintArtist[] Artists { get; set; } = [];
    [JsonProperty("duration")] public int Duration { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("releases")] public AcoustIdFingerprintReleaseGroups[]? Releases { get; set; } = [];
    [JsonProperty("sources")] public int Sources { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
}