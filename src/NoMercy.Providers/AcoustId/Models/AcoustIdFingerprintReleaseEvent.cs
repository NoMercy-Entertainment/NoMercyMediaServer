using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;
public class AcoustIdFingerprintReleaseEvent
{
    [JsonProperty("country")] public string Country { get; set; } = string.Empty;
    [JsonProperty("date")] public AcoustIdFingerprintDate Date { get; set; } = new();
}