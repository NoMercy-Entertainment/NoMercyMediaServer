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

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// One pre-encoded audio track a live session's master playlist advertises. A
/// NoMercy-encoded file already ships its audio as separate browser-ready HLS
/// renditions, so the live session only transcodes the (HEVC) video and points
/// each audio track at the file's own rendition — no audio is re-encoded and the
/// player switches tracks instantly.
/// </summary>
/// <param name="Language">ISO language tag as stored on the file (e.g. "eng").</param>
/// <param name="Uri">Client-facing URL of the rendition's <c>.m3u8</c>.</param>
/// <param name="IsDefault">Whether the player opens this track by default.</param>
public record LiveAudioRendition(string Language, string Uri, bool IsDefault);
