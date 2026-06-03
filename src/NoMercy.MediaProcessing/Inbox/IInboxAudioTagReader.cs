namespace NoMercy.MediaProcessing.Inbox;

public interface IInboxAudioTagReader
{
    Task<InboxAudioTags?> ReadAsync(string path, Ulid driverId, CancellationToken ct);
}
