using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record PathRequest
{
    [JsonProperty("folder")] public string Folder { get; set; } = string.Empty;
}