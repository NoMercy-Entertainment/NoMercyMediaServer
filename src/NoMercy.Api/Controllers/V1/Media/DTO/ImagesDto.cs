using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record ImagesDto
{
    [JsonProperty("profiles")] public ImageDto[] Profiles { get; set; } = [];
}