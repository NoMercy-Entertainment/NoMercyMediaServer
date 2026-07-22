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

using System.Text.RegularExpressions;

namespace NoMercy.Storage.Common;

/// <summary>
/// Shared glob-style pattern matcher for storage drivers. Translates the
/// <c>*</c> / <c>?</c> wildcards used by directory listings into a regex and
/// matches case-insensitively. Previously duplicated verbatim in the NFS, S3,
/// and WebDAV drivers.
/// </summary>
internal static class StoragePatternMatcher
{
    public static bool Matches(string name, string pattern)
    {
        if (pattern == "*" || string.IsNullOrEmpty(value: pattern))
            return true;

        string regexPattern =
            "^"
            + string.Concat(
                values: pattern.Select(selector: c =>
                    c switch
                    {
                        '*' => ".*",
                        '?' => ".",
                        '.' => "\\.",
                        _ => Regex.Escape(str: c.ToString()),
                    }
                )
            )
            + "$";

        return Regex.IsMatch(input: name, pattern: regexPattern, options: RegexOptions.IgnoreCase);
    }
}
