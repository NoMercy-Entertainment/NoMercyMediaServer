using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record LogoDto
{
    [JsonProperty("aspectRatio")] public double AspectRatio { get; set; }
    [JsonProperty("src")] public string Src { get; set; } = string.Empty;
}