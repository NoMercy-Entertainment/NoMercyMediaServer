using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record ServerActivityRequest
{
    [JsonProperty("take")] public int? Take { get; set; } = 10;
}