namespace NoMercy.Storage;

public readonly record struct StorageEntryInfo(
    string Path,
    bool IsDirectory,
    long Size,
    DateTime LastWriteUtc
);
