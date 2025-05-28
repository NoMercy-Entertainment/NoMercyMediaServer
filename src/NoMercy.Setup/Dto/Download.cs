using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;
public class Download
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
    [JsonProperty("url")] public Uri? Url { get; set; }
    [JsonProperty("filter")] public string Filter { get; set; } = string.Empty;
    [JsonProperty("no_delete")] public bool NoDelete { get; set; }
    [JsonProperty("last_updated")] public string LastUpdated { get; set; } = string.Empty;
}