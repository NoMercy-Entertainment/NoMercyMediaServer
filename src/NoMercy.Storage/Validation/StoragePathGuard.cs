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
    /// Validates <paramref name="requestedPath"/> and returns its
    /// canonical absolute form. Throws
    /// <see cref="StoragePathNotAllowedException"/> on rejection.
    /// </summary>
    public string Validate(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
            throw new StoragePathNotAllowedException(requestedPath ?? "<null>", "path is empty");

        if (requestedPath.Contains('\0'))
            throw new StoragePathNotAllowedException(requestedPath, "null byte in path");

        if (
            OperatingSystem.IsWindows()
            && (
                requestedPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                || requestedPath.StartsWith(@"\\.\", StringComparison.Ordinal)
            )
        )
            throw new StoragePathNotAllowedException(requestedPath, "device paths are not allowed");

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
