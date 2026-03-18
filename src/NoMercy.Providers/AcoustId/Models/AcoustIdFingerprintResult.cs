using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;

public class AcoustIdFingerprintResult
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("recordings")] public AcoustIdFingerprintRecording?[]? Recordings { get; set; }
    [JsonProperty("score")] public double Score { get; set; }
}