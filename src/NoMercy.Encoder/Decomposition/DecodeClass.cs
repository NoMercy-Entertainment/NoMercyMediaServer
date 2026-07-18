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

namespace NoMercy.Encoder.Decomposition;

/// <summary>
/// What a video output costs the source decode. A full ffmpeg decode is the
/// real cost of an encode, not the rung count — <see cref="DecodeAwareBundlePlanner"/>
/// classifies every video output by this before it decides how many ffmpeg
/// invocations the run needs.
/// </summary>
public enum DecodeClass
{
    /// <summary>
    /// Stream-copied video (<c>EncoderName == "copy"</c>). Needs no decode at
    /// all — the bytes are passed through. Rides whichever decode group
    /// already exists for free, or forms its own zero-decode "remux" unit
    /// when the plan has no real decode group at all.
    /// </summary>
    Copy,

    /// <summary>
    /// A real encoder with no HDR→SDR tonemap. Every Transcode rung off the
    /// same source shares ONE decode via the filtergraph split the builder
    /// already emits.
    /// </summary>
    Transcode,

    /// <summary>
    /// A real encoder with <see cref="Output.VideoOutputPlan.ConvertHdrToSdr"/>
    /// set and a non-empty <see cref="Output.VideoOutputPlan.TonemapFilterChain"/>.
    /// The HDR→Rec.709 pass runs once; its single <c>[sdr]</c> intermediate
    /// feeds every SDR-tonemap rung sharing the same chain AND the thumbnail
    /// sprite (correct Rec.709 color, one decode instead of a separate pass
    /// per consumer). Kept as its own class rather than merged with
    /// <see cref="Transcode"/> so a plan mixing an HDR-preserve rung with an
    /// SDR-tonemap rung gets two clearly separate decode groups instead of
    /// silently sharing one via <c>FilterGraphAssembler</c>'s dedupe path.
    /// </summary>
    Tonemap,
}
