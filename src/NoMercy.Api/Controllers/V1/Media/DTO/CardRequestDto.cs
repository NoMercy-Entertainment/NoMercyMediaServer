using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record CardRequestDto
{
    [JsonProperty("replace_id")] public Ulid ReplaceId { get; set; }
}