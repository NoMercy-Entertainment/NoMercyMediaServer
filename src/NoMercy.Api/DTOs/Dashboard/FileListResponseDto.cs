using Newtonsoft.Json;
using NoMercy.MediaProcessing.Files;

namespace NoMercy.Api.DTOs.Dashboard;

public record FileListResponseDto
{
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("files")] public List<FileItem> Files { get; set; } = new();
}