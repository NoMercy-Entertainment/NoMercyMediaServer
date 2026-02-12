namespace NoMercy.Plugins.Abstractions;

public class PluginInfo
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Version Version { get; init; }
    public required PluginStatus Status { get; set; }
    public string? Author { get; init; }
    public string? ProjectUrl { get; init; }
    public string? AssemblyPath { get; init; }
}
