using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record SetupResponseDto
{
    [JsonProperty("setup_complete")] public bool SetupComplete { get; set; }
}