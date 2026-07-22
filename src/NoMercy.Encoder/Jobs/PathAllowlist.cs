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

namespace NoMercy.Encoder.Jobs;

public record PathAllowlist(string[] AllowedInputPaths, string[] AllowedOutputPaths)
{
    public bool IsInputPathAllowed(string path) => IsUnderAny(path: path, allowedRoots: AllowedInputPaths);

    public bool IsOutputPathAllowed(string path) => IsUnderAny(path: path, allowedRoots: AllowedOutputPaths);

    /// <summary>
    /// Returns true when <paramref name="path"/> resolves to either an
    /// allowed root itself or a descendant of one. Comparisons happen
    /// after <see cref="Path.GetFullPath(string)"/> normalization so
    /// <c>..</c> traversal is collapsed first. Matching requires a
    /// directory-separator boundary so the allowlist entry
    /// <c>/media/movies</c> does NOT also grant access to the sibling
    /// <c>/media/movies_private</c> — a regression the old StartsWith
    /// check was vulnerable to.
    /// </summary>
    private static bool IsUnderAny(string path, string[] allowedRoots)
    {
        if (allowedRoots.Length == 0)
            return false;

        string normalized = Path.GetFullPath(path: path);

        foreach (string allowed in allowedRoots)
        {
            string root = Path.GetFullPath(path: allowed)
                .TrimEnd(trimChars: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);

            // Exact match: the path is the root itself.
            if (normalized.Equals(value: root, comparisonType: StringComparison.OrdinalIgnoreCase))
                return true;

            // Descendant: the path must continue with a directory separator
            // immediately after the root prefix. Without the separator check,
            // "/media/movies" would let "/media/movies_private" through.
            string rootWithSep = root + Path.DirectorySeparatorChar;
            if (normalized.StartsWith(value: rootWithSep, comparisonType: StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
