using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record SourceDto
{
    [JsonProperty("src")] public string Src { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("languages")] public string?[]? Languages { get; set; } = [];
}