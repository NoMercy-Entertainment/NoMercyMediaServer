using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record DirectoryRequest
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
}