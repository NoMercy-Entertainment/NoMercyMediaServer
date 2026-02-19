namespace NoMercy.Events.Configuration;

public sealed class ConfigurationChangedEvent : EventBase
{
    public override string Source => "Configuration";

    public required string Section { get; init; }
    public required string Key { get; init; }
    public Guid? ChangedByUserId { get; init; }
}
