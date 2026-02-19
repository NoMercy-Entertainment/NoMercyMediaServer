using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record CardRequestDto
{
    [JsonProperty("replace_id")] public Ulid ReplaceId { get; set; }
}