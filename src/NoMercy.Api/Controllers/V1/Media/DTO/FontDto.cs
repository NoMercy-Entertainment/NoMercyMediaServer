using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record FontDto
{
    [JsonProperty("file")] public string File { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
}