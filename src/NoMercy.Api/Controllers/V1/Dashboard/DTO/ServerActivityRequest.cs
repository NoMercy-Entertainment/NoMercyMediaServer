using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record ServerActivityRequest
{
    [JsonProperty("take")] public int? Take { get; set; } = 10;
}