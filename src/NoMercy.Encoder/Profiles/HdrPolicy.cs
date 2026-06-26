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

namespace NoMercy.Encoder.Profiles;

public enum HdrPolicy
{
    PassthroughWhenPossible,
    AlwaysTonemap,
    AlwaysPreserve,

    /// <summary>
    /// When the source is HDR, split coverage along the bit-depth / codec
    /// role: 10-bit rungs (HEVC Main10) preserve HDR via passthrough;
    /// 8-bit rungs (H.264) carry the tonemapped SDR copy. Each rung emits
    /// ONE output — no per-rung HDR+SDR doubling. SDR coverage at a given
    /// height therefore requires an 8-bit rung at that height (configure
    /// via <c>AutoLadder.H264FallbackHeights</c>); SDR-only clients above
    /// the highest 8-bit rung step down. When the source is SDR, behaves
    /// like AlwaysTonemap (single SDR output per resolution).
    /// </summary>
    EmitHdrAndSdr,
}
