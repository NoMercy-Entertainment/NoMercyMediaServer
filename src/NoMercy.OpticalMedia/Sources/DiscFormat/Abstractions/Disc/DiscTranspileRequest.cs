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

namespace NoMercy.DiscFormat.Abstractions.Disc;

/// One language the disc actually carries, normalized to a 639-1 code and the language's native
/// display label. The pre-menu language picker is built from these; the set is the disc's own
/// (its STN/MPLS subtitle languages), never a hardcoded menu.
public sealed record DiscLocale
{
    public required string Code { get; init; }
    public required string Label { get; init; }
}

/// Everything the transpiler needs to compile one disc, all produced by the build pipeline in a
/// working directory. The transpiler reads these and returns a manifest; it writes nothing to disk,
/// so it never pollutes its own source tree. Fields are source-specific: a DVD request fills the IFO
/// group. Each transpiler reads only the group its CanHandle kind owns.
public sealed record DiscTranspileRequest
{
    public required DiscKind Kind { get; init; }
    public required string DiscTitle { get; init; }

    /// The disc's structural identity, resolved by the pipeline's IDiscIdentityReader before the
    /// transpile. When present it becomes the manifest disc id, so a swapped disc is detectable;
    /// when absent the provider falls back to a title slug (identity-less callers only).
    public DiscIdentity? Identity { get; init; }

    /// The distinct locales the disc carries, in the disc's own stream order, derived from its
    /// STN/MPLS language set by the pipeline. When the disc presents a language-select boot screen
    /// these drive the emitted pre-menu; empty when the disc has no locale set to choose from.
    public IReadOnlyList<DiscLocale> Locales { get; init; } = [];

    /// Presentation-relative feature chapter seconds for scene selection, from the pipeline.
    public IReadOnlyList<double> ChapterMarks { get; init; } = [];

    /// The DVD-Video IFO files to parse (VIDEO_TS.IFO plus each VTS_xx_0.IFO), keyed by file name.
    /// Supplied as raw bytes by the pipeline, which owns reading the disc; the transpiler parses
    /// these per the public IFO spec and never touches the drive itself. Null for non-DVD discs.
    public IReadOnlyDictionary<string, byte[]>? IfoFiles { get; init; }

    /// The mounted Blu-ray device/image path (e.g. "D:/"), needed to read the AACS disc identity
    /// via libbluray for the structural fingerprint. Null for DVD/IFO-only requests.
    public string? DevicePath { get; init; }

    /// The parsed HDMV menu structures (MovieObjects + IG pages + palettes the pipeline extracted
    /// from the disc's M2TS). Null for DVD/BD-J requests; empty when an HDMV title has no menu.
    public HdmvTitleData? Hdmv { get; init; }

    /// The client-facing HLS base URL for BD-J structural bundles (e.g. "https://server/media/disc").
    /// The structural transpiler uses this to build reel HLS paths; empty string = relative paths.
    public string? HlsBaseUrl { get; init; }

    /// Optional directory of pre-extracted .sup files named `<reelId>.<lang>.sup`.
    /// When present the structural transpiler decodes each file into image cues and embeds
    /// them in the bundle under assets/subs/<lang>/.
    public string? SubtitleSupDirectory { get; init; }
}
