using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record MkdirResponse
{
    [JsonProperty("status")]
    public string Status { get; set; } = "ok";

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;
}
