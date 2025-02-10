
using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;

public class AcoustIdFingerprint
{
    [JsonProperty("results")] public AcoustIdFingerprintResult[] Results { get; set; } = [];
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
}
