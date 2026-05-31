namespace NoMercy.Events.Library;

/// <summary>
/// Published by <c>FileRescanJob</c> after video / audio files for a single
/// media item (movie or episode) have been scanned and their <c>VideoFile</c>
/// rows written to the database.
///
/// Consumers of this event know the files are queryable by <c>MediaId</c> and
/// can schedule follow-up work like automatic encoding.
/// </summary>
public sealed class MediaFilesScannedEvent : EventBase
{
    public override string Source => "FileRescan";

    /// <summary>Movie id or episode id whose files just landed.</summary>
    public required int MediaId { get; init; }

    public required Ulid LibraryId { get; init; }
}
