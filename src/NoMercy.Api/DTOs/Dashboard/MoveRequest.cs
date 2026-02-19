using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record MoveRequest
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
}