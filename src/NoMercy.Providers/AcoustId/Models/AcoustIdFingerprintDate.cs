using Newtonsoft.Json;

namespace NoMercy.Providers.AcoustId.Models;

public class AcoustIdFingerprintDate
{
    [JsonProperty("day")] public int Day { get; set; }
    [JsonProperty("month")] public int Month { get; set; }
    [JsonProperty("year")] public int Year { get; set; }
}