using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record MoveRequest
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
}