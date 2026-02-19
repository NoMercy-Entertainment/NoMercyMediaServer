using System.Text.Json.Serialization;

namespace NoMercy.Plugins.Abstractions;

public class PluginManifest
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("targetAbi")]
    public string? TargetAbi { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("projectUrl")]
    public string? ProjectUrl { get; init; }

    [JsonPropertyName("assembly")]
    public required string Assembly { get; init; }

    [JsonPropertyName("autoEnabled")]
    public bool AutoEnabled { get; init; } = true;
}
