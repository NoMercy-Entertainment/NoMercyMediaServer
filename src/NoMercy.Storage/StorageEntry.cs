namespace NoMercy.Storage;

public sealed record StorageEntry(
    string Path,
    bool IsDirectory,
    long SizeBytes,
    DateTimeOffset LastModified
);
