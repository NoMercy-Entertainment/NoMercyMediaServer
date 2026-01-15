using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record AddFilesRequest
{
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
    [JsonProperty("files")] public AddFile[] Files { get; set; } = [];
}

public record AddFile
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
    [JsonProperty("id")] public string Id { get; set; } = null!;
}