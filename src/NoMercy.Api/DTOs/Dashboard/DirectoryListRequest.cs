using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record DirectoryListRequest
{
    [JsonProperty("folder")]
    public string Folder { get; set; } = string.Empty;

    [JsonProperty("with_empty")]
    public bool WithEmpty { get; set; }
}
