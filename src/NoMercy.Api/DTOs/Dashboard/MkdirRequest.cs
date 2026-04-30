using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record MkdirRequest
{
    [JsonProperty("parent")]
    public string Parent { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
