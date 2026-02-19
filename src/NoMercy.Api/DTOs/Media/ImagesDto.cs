using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record ImagesDto
{
    [JsonProperty("profiles")] public ImageDto[] Profiles { get; set; } = [];
}