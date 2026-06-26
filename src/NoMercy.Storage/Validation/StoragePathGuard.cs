// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
    /// Windows device paths. Does NOT canonicalize, does NOT enforce the
    /// under-root rule, and does NOT reject OS-absolute paths — that check
    /// is the caller's responsibility when operating in a remote-driver
    /// context (see <see cref="RejectAbsolutePath"/>).
    /// Empty/null is accepted — drivers treat "" as the scope root (Rule 3).
    /// </summary>
    public static void StructuralValidate(string? requestedPath)
    {
        if (requestedPath is null || requestedPath.Length == 0)
            return;

        if (requestedPath.Contains('\0'))
            throw new StoragePathNotAllowedException(requestedPath, "null byte in path");

        if (
            requestedPath == ".."
            || requestedPath.StartsWith("../", StringComparison.Ordinal)
            || requestedPath.StartsWith("..\\", StringComparison.Ordinal)
            || requestedPath.EndsWith("/..", StringComparison.Ordinal)
            || requestedPath.EndsWith("\\..", StringComparison.Ordinal)
            || requestedPath.Contains("/../", StringComparison.Ordinal)
            || requestedPath.Contains(@"\..\", StringComparison.Ordinal)
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
    /// Rejects any path that is rooted in OS or backend-absolute terms.
    /// These forms are never valid scope-relative keys for remote drivers:
    ///   - leading '/' or '\'  (Unix root, UNC share, or Windows backslash root)
    ///   - Windows drive prefix  X:
    /// Must be called by remote-driver entry points (e.g. RemoteStorage.V())
    /// in addition to <see cref="StructuralValidate"/>. Not called from
    /// <see cref="Validate"/> because LocalStorage passes OS-absolute paths
    /// through the structural check before the under-root allowlist check.
    /// </summary>
    public static void RejectAbsolutePath(string path)
    {
        if (path.Length == 0)
            return;

        char first = path[0];
        if (first == '/' || first == '\\')
            throw new StoragePathNotAllowedException(
                path,
                "absolute paths are not allowed as scope-relative keys"
            );

        if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
            throw new StoragePathNotAllowedException(
                path,
                "absolute paths are not allowed as scope-relative keys"
            );
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

    // Allowlist roots are on-disk OS paths; canonicalizing them here is the
    // local-enforcement boundary, not a storage-contract path (NMS002).
#pragma warning disable NMS002
    private static string NormalizeRoot(string root) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
#pragma warning restore NMS002

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
