using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record MetaDto
{
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("logo")] public LogoDto? Logo { get; set; } = new();
}