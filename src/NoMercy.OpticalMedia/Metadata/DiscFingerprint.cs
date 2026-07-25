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
        sb.Append(info.Titles.Length).Append('|');
        foreach (DiscTitle title in info.Titles.OrderBy(t => t.Index))
        {
            sb.Append(title.Index)
                .Append(':')
                .Append(((long)title.Duration.TotalSeconds).ToString(CultureInfo.InvariantCulture))
                .Append(';');
        }

        byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }
}
