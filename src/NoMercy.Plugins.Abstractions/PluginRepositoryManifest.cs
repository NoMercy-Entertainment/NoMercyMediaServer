using System.Text.Json.Serialization;

namespace NoMercy.Plugins.Abstractions;

public class PluginRepositoryManifest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("plugins")]
    public required List<PluginRepositoryEntry> Plugins { get; init; }
}
