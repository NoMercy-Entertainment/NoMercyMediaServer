using Newtonsoft.Json;
using NoMercy.Database;

namespace NoMercy.Api.Controllers.V1.DTO;

public record ColorPalettesDto
{
    [JsonProperty("logo")] public IColorPalettes Logo { get; set; } = new();
    [JsonProperty("poster")] public IColorPalettes Poster { get; set; } = new();
    [JsonProperty("backdrop")] public IColorPalettes Backdrop { get; set; } = new();
}