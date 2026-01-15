using Newtonsoft.Json;
using NoMercy.MediaProcessing.Files;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record FileListResponseDto
{
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("files")] public List<FileItem> Files { get; set; } = new();
}