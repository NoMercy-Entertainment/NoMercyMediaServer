namespace NoMercy.Events.Plugins;

public sealed class PluginErrorEvent : EventBase
{
    public override string Source => "PluginManager";

    public required string PluginId { get; init; }
    public required string PluginName { get; init; }
    public required string ErrorMessage { get; init; }
    public string? ExceptionType { get; init; }
}
