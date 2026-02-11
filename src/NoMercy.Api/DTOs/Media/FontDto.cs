using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record FontDto
{
    [JsonProperty("file")] public string File { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
}