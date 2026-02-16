namespace NoMercy.Events.Music;

public sealed class MusicItemLikedEvent : EventBase
{
    public override string Source => "Music";

    public required Guid UserId { get; init; }
    public required Guid ItemId { get; init; }
    public required string ItemType { get; init; }
    public required bool Liked { get; init; }
}
