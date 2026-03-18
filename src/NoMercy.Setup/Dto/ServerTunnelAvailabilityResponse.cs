using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;

public class ServerTunnelAvailabilityResponse
{
    [JsonProperty("status")] public string Status { get; set; } = null!;
    [JsonProperty("message")] public string? Message { get; set; }
    [JsonProperty("allowed")] public bool Allowed { get; set; }
    [JsonProperty("token")] public string? Token { get; set; }
}