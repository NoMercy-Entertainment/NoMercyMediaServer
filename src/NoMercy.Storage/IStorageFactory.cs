namespace NoMercy.Storage;

/// <summary>
/// Creates and caches <see cref="IStorage"/> instances keyed on
/// <see cref="Folder.Id"/>. Each instance is scoped to one folder root,
/// so the path guard prevents any consumer from escaping that root into
/// an unrelated part of the filesystem (or a different cloud bucket).
/// </summary>
public interface IStorageFactory
{
    /// <summary>
    /// Returns an <see cref="IStorage"/> configured for the given folder.
    /// <paramref name="driverId"/> is always required — every folder must
    /// have an assigned driver. The driver type and config are resolved via
    /// <see cref="IDriverConfigResolver"/>. <paramref name="subPath"/> is
    /// the folder-relative sub-path inside the driver root; pass an empty
    /// string for the driver root itself. Instances are cached on
    /// <c>(folderId, driverType, configJsonHash)</c>.
    /// </summary>
    IStorage For(Ulid folderId, Ulid driverId, string subPath);

    /// <summary>
    /// Drops every cached <see cref="IStorage"/> whose key starts with
    /// <paramref name="folderId"/>. Call when a folder's driver changes
    /// or when the folder is deleted.
    /// </summary>
    void Invalidate(Ulid folderId);

    /// <summary>
    /// Drops every cached <see cref="IStorage"/>. Call on host shutdown.
    /// </summary>
    void InvalidateAll();
}
