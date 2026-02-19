using Newtonsoft.Json;

namespace NoMercy.Cli.Models;

internal class PluginResponse
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description")] public string Description { get; set; } = string.Empty;
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("author")] public string Author { get; set; } = string.Empty;
    [JsonProperty("project_url")] public string? ProjectUrl { get; set; }
}
