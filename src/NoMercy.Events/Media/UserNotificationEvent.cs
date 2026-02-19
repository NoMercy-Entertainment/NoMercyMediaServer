namespace NoMercy.Events.Media;

public sealed class UserNotificationEvent : EventBase
{
    public override string Source => "Media";

    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Type { get; init; }
    public string Hub { get; init; } = "videoHub";
}
