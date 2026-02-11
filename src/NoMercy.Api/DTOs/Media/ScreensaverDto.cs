using Newtonsoft.Json;


namespace NoMercy.Api.DTOs.Media;

public record ScreensaverDto
{
    [JsonProperty("data")] public IEnumerable<ScreensaverDataDto> Data { get; set; } = [];
}