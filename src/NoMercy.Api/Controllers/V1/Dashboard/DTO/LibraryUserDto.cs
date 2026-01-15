using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record LibraryUserDto
{
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    [JsonProperty("UserId")] public Guid UserId { get; set; }
}