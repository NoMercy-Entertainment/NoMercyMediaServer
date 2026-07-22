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

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Stable per-disc identifier computed from title count and per-title
/// durations. Two physically identical discs give identical fingerprints
/// regardless of metadata, drive, or read path. Used to cache TMDB
/// matches across re-inserts and across users.
/// </summary>
public static class DiscFingerprint
{
    /// <summary>
    /// Computes the SHA1 fingerprint as an uppercase hex string.
    /// Returns empty string for a disc with no titles.
    /// </summary>
    public static string Compute(DiscInfo info)
    {
        if (info.Titles.Length == 0)
            return string.Empty;

        StringBuilder sb = new();
        sb.Append(value: info.Titles.Length).Append(value: '|');
        foreach (DiscTitle title in info.Titles.OrderBy(keySelector: t => t.Index))
        {
            sb.Append(value: title.Index)
                .Append(value: ':')
                .Append(value: ((long)title.Duration.TotalSeconds).ToString(provider: CultureInfo.InvariantCulture))
                .Append(value: ';');
        }

        byte[] hash = SHA1.HashData(source: Encoding.UTF8.GetBytes(s: sb.ToString()));
        return Convert.ToHexString(inArray: hash);
    }
}
