using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record LibraryUserDto
{
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    [JsonProperty("UserId")] public Guid UserId { get; set; }
}