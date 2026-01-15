using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record PathRequest
{
    [JsonProperty("folder")] public string Folder { get; set; } = string.Empty;
}