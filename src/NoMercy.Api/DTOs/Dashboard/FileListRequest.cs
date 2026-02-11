using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record FileListRequest
{
    [JsonProperty("folder")] public string Folder { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
}