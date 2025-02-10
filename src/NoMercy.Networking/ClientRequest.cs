using Newtonsoft.Json;

namespace NoMercy.Networking;
public class ClientRequest
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("browser")] public string Browser { get; set; } = string.Empty;
    [JsonProperty("os")] public string Os { get; set; } = string.Empty;
    [JsonProperty("device")] public string Device { get; set; } = string.Empty;
    [JsonProperty("custom_name")] public string CustomName { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
}