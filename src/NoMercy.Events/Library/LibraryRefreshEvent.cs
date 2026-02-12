namespace NoMercy.Events.Library;

public sealed class LibraryRefreshEvent : EventBase
{
    public override string Source => "LibraryRefresh";

    public required dynamic?[] QueryKey { get; init; }
}
