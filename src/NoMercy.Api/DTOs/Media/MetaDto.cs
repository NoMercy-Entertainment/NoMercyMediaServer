using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record MetaDto
{
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("logo")] public LogoDto? Logo { get; set; } = new();
}