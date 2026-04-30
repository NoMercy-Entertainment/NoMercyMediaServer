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
    /// Cached on <c>(folderId, backendType, configJsonHash)</c> — repeated
    /// calls with the same triple return the same instance. A config change
    /// produces a different hash and therefore a fresh instance without
    /// needing an explicit <see cref="Invalidate"/> call.
    /// </summary>
    IStorage For(Ulid folderId, string backendType, string? backendConfigJson, string folderPath);

    /// <summary>
    /// Drops every cached <see cref="IStorage"/> whose key starts with
    /// <paramref name="folderId"/>. Call when a folder's backend config
    /// changes or when the folder is deleted.
    /// </summary>
    void Invalidate(Ulid folderId);

    /// <summary>
    /// Drops every cached <see cref="IStorage"/>. Call on host shutdown.
    /// </summary>
    void InvalidateAll();
}
