namespace NoMercy.Storage.DriverGrouping;

/// <summary>
/// Identifies the logical storage endpoint a folder root belongs to.
/// Used to ensure folders on different endpoints are never merged into
/// one driver.
///
/// Examples:
///   UNC share  → Key = "\\192.168.1.1\Media"
///   Win drive  → Key = "C:"
///   POSIX      → Key = "/" (or mount root)
/// </summary>
public sealed record StorageEndpoint(string Key, StorageEndpointKind Kind);
