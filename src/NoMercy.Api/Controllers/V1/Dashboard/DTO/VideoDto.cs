using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;
public record VideoDto
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("width")] public int? Width { get; set; }
    [JsonProperty("height")] public int? Height { get; set; }
}