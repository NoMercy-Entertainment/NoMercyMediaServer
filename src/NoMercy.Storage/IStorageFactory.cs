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
    /// When <paramref name="driverId"/> is <c>null</c>, the built-in local
    /// driver is used with <paramref name="folderPath"/> as the root.
    /// When non-null, the named driver is resolved via
    /// <see cref="IDriverConfigResolver"/> and the appropriate backend is
    /// constructed. Instances are cached on
    /// <c>(folderId, driverId, configJsonHash)</c>.
    /// </summary>
    IStorage For(Ulid folderId, Ulid? driverId, string folderPath);

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
