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

    /// <summary>
    /// Rebases a driver-absolute scan path (e.g.
    /// <c>"/mnt/vault/Media/Marvels/TV.Shows/What.If.(2021)/file.m3u8"</c>)
    /// onto its scope-relative folder root (<c>"Marvels/TV.Shows"</c>), yielding
    /// a facade-valid, backend-neutral key
    /// (<c>"Marvels/TV.Shows/What.If.(2021)/file.m3u8"</c>). MediaScan resolves
    /// every path through the driver, so it hands back absolute paths that the
    /// <see cref="IStorage"/> facade rejects on remote backends; rebasing gives
    /// callers a path the facade accepts and a portable value to persist.
    /// The folder root is matched as a substring; when it is absent the input is
    /// assumed already relative and returned with a trimmed leading slash.
    /// </summary>
    public static string RebaseToFolderRoot(string absolutePath, string folderPath)
    {
        string normalizedItem = absolutePath.Replace('\\', '/');
        string normalizedRoot = folderPath.Replace('\\', '/').Trim('/');

        if (normalizedRoot.Length == 0)
            return normalizedItem.TrimStart('/');

        int rootIndex = normalizedItem.IndexOf(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        return rootIndex < 0
            ? normalizedItem.TrimStart('/')
            : normalizedItem[rootIndex..].TrimStart('/');
    }
}
