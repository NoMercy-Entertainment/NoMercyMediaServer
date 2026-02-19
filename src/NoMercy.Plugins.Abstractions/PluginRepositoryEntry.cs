using System.Text.Json.Serialization;

namespace NoMercy.Plugins.Abstractions;

public class PluginRepositoryEntry
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("projectUrl")]
    public string? ProjectUrl { get; init; }

    [JsonPropertyName("versions")]
    public required List<PluginVersionEntry> Versions { get; init; }
}

public class PluginVersionEntry
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("targetAbi")]
    public string? TargetAbi { get; init; }

    [JsonPropertyName("downloadUrl")]
    public required string DownloadUrl { get; init; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName("changelog")]
    public string? Changelog { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }
}
