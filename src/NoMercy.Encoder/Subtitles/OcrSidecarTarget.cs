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

using NoMercy.Storage;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Where an OCR sidecar belongs, in the encode bundle's own terms.
///
/// The library scan pairs a bitmap subtitle with its text track by matching
/// <c>{lang}.{type}</c> across the two filenames (see
/// <c>FileManager.SubtitleFileRegex</c>), so an OCR sidecar only reaches a
/// player when it is named as the sibling of the <c>.mks</c>/<c>.sup</c> it was
/// read from — <c>{MediaTitle}.{lang}.{variant}.vtt</c>, the same
/// <c>subtitles/{filename}.{lang}.{type}</c> template the extraction pass uses.
/// A name the engine invents for itself parses as a different variant, leaves
/// the bitmap counted as orphaned, and shows up (if at all) as a bogus track.
/// </summary>
/// <param name="Storage">Storage <see cref="OutputDirectory"/> is addressed against.</param>
/// <param name="OutputDirectory">The encode's output directory, as a key for <see cref="Storage"/>.</param>
/// <param name="MediaTitle">Bundle filename stem, e.g. <c>Show.S01E01.Title.NoMercy</c>.</param>
/// <param name="Variant">full / sign / song / sdh / forced — must match the bitmap sidecar's.</param>
public record OcrSidecarTarget(
    IStorage Storage,
    string OutputDirectory,
    string MediaTitle,
    string Variant
);
