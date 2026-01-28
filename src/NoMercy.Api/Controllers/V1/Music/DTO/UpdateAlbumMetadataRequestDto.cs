using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public class UpdateMusicMetadataRequestDto
{
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
}