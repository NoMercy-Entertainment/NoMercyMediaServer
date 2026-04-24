namespace NoMercy.Storage;

/// <summary>
/// Configuration for <see cref="LocalStorage"/>. When
/// <see cref="AllowedRoots"/> is empty the path guard runs in a
/// permissive mode (only structural checks: empty path, null bytes,
/// Windows device paths). Once consumers have finished migrating to
/// <see cref="IStorage"/> the host should populate
/// <see cref="AllowedRoots"/> with the union of library roots, output
/// roots, scratch dirs, etc.
/// </summary>
public sealed class StorageOptions
{
    public List<string> AllowedRoots { get; set; } = [];
}
