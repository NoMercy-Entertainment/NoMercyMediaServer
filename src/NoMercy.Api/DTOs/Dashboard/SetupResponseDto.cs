using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record SetupResponseDto
{
    [JsonProperty("setup_complete")] public bool SetupComplete { get; set; }
}