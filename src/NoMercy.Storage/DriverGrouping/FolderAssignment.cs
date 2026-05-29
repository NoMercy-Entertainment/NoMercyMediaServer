namespace NoMercy.Storage.DriverGrouping;

/// <summary>
/// A folder's assignment within a <see cref="DriverGroup"/>.
/// </summary>
/// <param name="FolderId">The folder's stable ULID.</param>
/// <param name="SubPath">
/// Path relative to the driver root. Empty string means the folder IS
/// the driver root. Forward slashes for SMB, OS separator for local.
/// </param>
public sealed record FolderAssignment(Ulid FolderId, string SubPath);
