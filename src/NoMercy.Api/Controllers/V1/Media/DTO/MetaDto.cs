using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record MetaDto
{
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("logo")] public LogoDto LogoDto { get; set; }
}