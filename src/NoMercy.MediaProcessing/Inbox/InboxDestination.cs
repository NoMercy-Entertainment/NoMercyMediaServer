namespace NoMercy.MediaProcessing.Inbox;

public sealed class InboxDestination
{
    public Ulid LibraryId { get; set; }
    public Ulid FolderId { get; set; }
    public Ulid ProfileId { get; set; }
    public Ulid DriverId { get; set; }
    public string FolderPath { get; set; } = string.Empty;
}
