namespace NoMercy.Storage;

/// <summary>
/// Pure-string path helpers for storage-relative paths. Equivalent to
/// <c>System.IO.Path.GetFileName</c> / <c>GetDirectoryName</c> / <c>GetFileNameWithoutExtension</c>
/// but always splits on <c>'/'</c> (Rule 2 of the IStorage path contract),
/// never on <c>'\\'</c>. Use when no <see cref="IStorage"/> instance is
/// available (e.g. stateless generators called from both IStorage consumers
/// and tests).
/// </summary>
public static class StoragePathHelpers
{
    /// <summary>
    /// Returns the last forward-slash-delimited segment of
    /// <paramref name="path"/> — the storage equivalent of
    /// <c>System.IO.Path.GetFileName</c>.
    /// </summary>
    public static string GetName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;
        string trimmed = path.TrimEnd('/');
        int idx = trimmed.LastIndexOf('/');
        return idx < 0 ? trimmed : trimmed[(idx + 1)..];
    }

    /// <summary>
    /// Returns the parent directory segment of <paramref name="path"/> —
    /// the storage equivalent of <c>System.IO.Path.GetDirectoryName</c>.
    /// Returns null when the path has no parent (already the scope root).
    /// </summary>
    public static string? GetParent(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        string trimmed = path.TrimEnd('/');
        int idx = trimmed.LastIndexOf('/');
        if (idx < 0)
            return null;
        string parent = trimmed[..idx];
        return string.IsNullOrEmpty(parent) ? null : parent;
    }

    /// <summary>
    /// Returns the last segment of <paramref name="path"/> without its
    /// file extension — the storage equivalent of
    /// <c>System.IO.Path.GetFileNameWithoutExtension</c>.
    /// </summary>
    public static string GetNameWithoutExtension(string path)
    {
        string name = GetName(path);
        int dot = name.LastIndexOf('.');
        return dot < 0 ? name : name[..dot];
    }

    /// <summary>
    /// Joins <paramref name="parent"/> and <paramref name="child"/> with a
    /// single <c>'/'</c>, trimming redundant separators. Storage equivalent
    /// of <c>System.IO.Path.Combine</c>. Use <see cref="IStorage.CombinePath"/>
    /// instead when an <see cref="IStorage"/> instance is in scope.
    /// </summary>
    public static string Combine(string parent, string child)
    {
        if (string.IsNullOrEmpty(child))
            return parent;
        if (string.IsNullOrEmpty(parent))
            return child;
        string trimmedParent = parent.TrimEnd('/', '\\');
        string trimmedChild = child.TrimStart('/', '\\');
        return $"{trimmedParent}/{trimmedChild}";
    }
}
