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

using NoMercy.Encoder.BuildingBlocks;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Builds the FFmpeg <c>ass=</c> filter expression for burning ASS/SSA
/// subtitles permanently into the video stream.
///
/// <para>Path escaping is delegated to the shared
/// <see cref="FilterGraphPathEscaper"/> so every filtergraph call site
/// stays in sync.</para>
///
/// <para>The optional <paramref name="fontDirectory"/> hint tells libass
/// where to look for fonts before scanning system directories. Pass the
/// <c>fonts/</c> subdirectory that <see cref="NoMercy.Encoder.PostProcess.FontExtractor"/>
/// populates from MKV attachments so embedded fonts load correctly.</para>
/// </summary>
public sealed class AssBurnInFilterBuilder
{
    /// <summary>
    /// Builds the <c>ass=</c> filter expression.
    /// </summary>
    /// <param name="assFilePath">
    /// Absolute or relative path to the <c>.ass</c> / <c>.ssa</c> file.
    /// </param>
    /// <param name="fontDirectory">
    /// Optional path to a directory containing fonts. Forwarded as
    /// <c>fontsdir=&lt;path&gt;</c> to libass. May be null.
    /// </param>
    /// <returns>
    /// A filter expression such as
    /// <c>ass='/path/to.ass'</c> or
    /// <c>ass='/path/to.ass':fontsdir='/fonts'</c>.
    /// </returns>
    public string Build(string assFilePath, string? fontDirectory = null)
    {
        string escaped = FilterGraphPathEscaper.Escape(path: assFilePath);
        string filter = $"ass={escaped}";

        if (!string.IsNullOrWhiteSpace(value: fontDirectory))
        {
            string escapedFontDir = FilterGraphPathEscaper.Escape(path: fontDirectory);
            filter += $":fontsdir={escapedFontDir}";
        }

        return filter;
    }
}
