using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record Images
{
    [JsonProperty("profiles")] public ImageDto[] Profiles { get; set; } = [];
}