using Newtonsoft.Json;


namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record ScreensaverDto
{
    [JsonProperty("data")] public IEnumerable<ScreensaverDataDto> Data { get; set; } = [];
}
