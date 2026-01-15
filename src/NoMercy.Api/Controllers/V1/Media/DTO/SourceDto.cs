using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record SourceDto
{
    [JsonProperty("src")] public String Src { get; set; } = null!;
    [JsonProperty("type")] public string Type { get; set; } = null!;
    [JsonProperty("languages")] public string?[]? Languages { get; set; } = [];
}