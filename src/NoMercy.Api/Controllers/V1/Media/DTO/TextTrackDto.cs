using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record TextTrackDto
{
    [JsonProperty("label")] public string Label { get; set; } = string.Empty;
    [JsonProperty("file")] public string File { get; set; } = string.Empty;
    [JsonProperty("language")] public string Language { get; set; } = string.Empty;
    [JsonProperty("kind")] public string Kind { get; set; } = string.Empty;
}