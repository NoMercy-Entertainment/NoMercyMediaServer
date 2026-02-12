namespace NoMercy.Events.Plugins;

public sealed class PluginLoadedEvent : EventBase
{
    public override string Source => "PluginManager";

    public required string PluginId { get; init; }
    public required string PluginName { get; init; }
    public required string Version { get; init; }
}
