namespace NoMercy.Storage.Validation;

/// <summary>
/// Validates every path that flows through <see cref="IStorage"/>
/// before it reaches a driver. Two layers:
///   1. Structural — empty / null-byte / Windows device paths are
///      rejected unconditionally.
///   2. Allowlist — when configured, the canonical path (after symlink
///      resolution) must sit under one of the allowed roots.
/// Empty allowlist = structural checks only — used during the encoder
/// Phase 0 migration before consumers have populated their roots.
/// </summary>
public sealed class StoragePathGuard
{
    private readonly IStorageDriver _driver;
    private readonly string[] _normalizedRoots;
    private readonly StringComparison _comparison;

    public bool Enforced => _normalizedRoots.Length > 0;

    public IReadOnlyList<string> AllowedRoots => _normalizedRoots;

    public StoragePathGuard(IEnumerable<string> allowedRoots, IStorageDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        _normalizedRoots = (allowedRoots ?? [])
            .Where(static r => !string.IsNullOrWhiteSpace(r))
            .Select(NormalizeRoot)
            .Distinct(StringComparerFromComparison.For(_comparison))
            .ToArray();
    }

    /// <summary>
    /// Structural-only validation: rejects null bytes, ".." traversal, and
    /// Windows device paths. Does NOT canonicalize and does NOT enforce the
    /// under-root rule. Used by RemoteStorage where the storage scope is a
    /// URL/key prefix rather than an OS-rooted path, and where canonicalisation
    /// against a local filesystem is meaningless. Empty/null is accepted —
    /// callers translate that to "scope root" before calling, but a path
    /// that's still empty after resolution gets through here as a no-op.
    /// </summary>
    public static void StructuralValidate(string? requestedPath)
    {
        if (requestedPath is null)
            return;

        if (requestedPath.Contains('\0'))
            throw new StoragePathNotAllowedException(requestedPath, "null byte in path");

        // ".." traversal — anywhere in the path. Keeps us safe even when a
        // remote driver doesn't canonicalize.
        if (
            requestedPath == ".."
            || requestedPath.StartsWith("../", StringComparison.Ordinal)
            || requestedPath.StartsWith("..\\", StringComparison.Ordinal)
            || requestedPath.EndsWith("/..", StringComparison.Ordinal)
            || requestedPath.EndsWith("\\..", StringComparison.Ordinal)
            || requestedPath.Contains("/../", StringComparison.Ordinal)
            || requestedPath.Contains("\\..\\", StringComparison.Ordinal)
        )
            throw new StoragePathNotAllowedException(requestedPath, ".. traversal is not allowed");

        if (
            OperatingSystem.IsWindows()
            && (
                requestedPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                || requestedPath.StartsWith(@"\\.\", StringComparison.Ordinal)
            )
        )
            throw new StoragePathNotAllowedException(requestedPath, "device paths are not allowed");
    }

    /// <summary>
    /// Validates <paramref name="requestedPath"/> and returns its
    /// canonical absolute form. Throws
    /// <see cref="StoragePathNotAllowedException"/> on rejection.
    ///
    /// Path Contract Rule 3 (empty = root) is honoured at the IStorage level
    /// (LocalStorage.ValidateScoped resolves empty → root before calling here)
    /// — Validate itself still rejects empty so callers can't accidentally
    /// bypass scope resolution.
    /// </summary>
    public string Validate(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
            throw new StoragePathNotAllowedException(requestedPath ?? "<null>", "path is empty");

        StructuralValidate(requestedPath);

        string canonical;
        try
        {
            canonical = _driver.GetFullPath(requestedPath);
        }
        catch (Exception ex)
        {
            throw new StoragePathNotAllowedException(
                requestedPath,
                $"cannot canonicalize: {ex.Message}"
            );
        }

        if (!Enforced)
            return canonical;

        string resolved = _driver.ResolveLinkTarget(canonical) ?? canonical;

        foreach (string root in _normalizedRoots)
            if (IsUnderRoot(resolved, root, _comparison))
                return canonical;

        throw new StoragePathNotAllowedException(
            requestedPath,
            $"path is not under any allowed root: [{string.Join(", ", _normalizedRoots)}]"
        );
    }

    private static string NormalizeRoot(string root) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    private static bool IsUnderRoot(string fullPath, string root, StringComparison cmp)
    {
        if (string.Equals(fullPath, root, cmp))
            return true;
        string withSep = root + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(withSep, cmp);
    }
}

internal static class StringComparerFromComparison
{
    public static StringComparer For(StringComparison c) =>
        c switch
        {
            StringComparison.Ordinal => StringComparer.Ordinal,
            StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase,
            StringComparison.CurrentCulture => StringComparer.CurrentCulture,
            StringComparison.CurrentCultureIgnoreCase => StringComparer.CurrentCultureIgnoreCase,
            StringComparison.InvariantCulture => StringComparer.InvariantCulture,
            StringComparison.InvariantCultureIgnoreCase =>
                StringComparer.InvariantCultureIgnoreCase,
            _ => StringComparer.Ordinal,
        };
}
