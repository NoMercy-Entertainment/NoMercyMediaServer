using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record DirectoryRequest
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
}