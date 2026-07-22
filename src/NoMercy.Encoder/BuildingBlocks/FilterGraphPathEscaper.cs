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

namespace NoMercy.Encoder.BuildingBlocks;

/// <summary>
/// Escapes a filesystem path for safe use as an FFmpeg filtergraph option
/// value (e.g. <c>lut3d=</c>, <c>ass=</c>, <c>subtitles=</c>), then wraps it
/// in single quotes so the graph-level delimiters (<c>,</c> <c>;</c> <c>[</c>
/// <c>]</c> and whitespace) are treated as literal content rather than
/// filtergraph syntax.
///
/// <para>Verified empirically against a real ffmpeg build: quoting alone is
/// NOT sufficient — an un-escaped drive-letter colon inside a quoted value
/// makes ffmpeg's <c>subtitles=</c> option parser misread the option
/// boundary entirely (it reports a spurious <c>original_size</c> parse
/// failure). Escaping the same value WITHOUT the surrounding quotes is
/// equally broken — the graph-level comma/semicolon/bracket delimiters
/// desync the option chain. Both quoting AND escaping <c>\</c> / <c>:</c> /
/// <c>'</c> inside the quotes are required together.</para>
///
/// <para>This is the single shared implementation for every call site that
/// splices a user-controlled path into a filtergraph. Do not duplicate this
/// logic locally.</para>
/// </summary>
public static class FilterGraphPathEscaper
{
    /// <summary>
    /// Normalises Windows separators to forward slashes, backslash-escapes
    /// <c>\</c>, <c>:</c>, and <c>'</c>, then wraps the result in single
    /// quotes ready to splice directly after a filter's <c>=</c>.
    /// </summary>
    public static string Escape(string path)
    {
        ArgumentNullException.ThrowIfNull(argument: path);

        // Normalise Windows separators first so the backslash-doubling
        // pass only sees intentional backslashes (none on a pure path).
        string normalised = path.Replace(oldChar: '\\', newChar: '/');

        // Order is critical: \ -> \\ must precede escaping other chars.
        string escaped = normalised
            .Replace(oldValue: "\\", newValue: @"\\") // \ -> \\ (no-op on normalised path)
            .Replace(oldValue: "'", newValue: "\\'") // ' -> \' (apostrophe in filename)
            .Replace(oldValue: ":", newValue: "\\:"); // : -> \: (drive letter / stream specifier)

        return $"'{escaped}'";
    }
}
