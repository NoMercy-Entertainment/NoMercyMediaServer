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

using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public enum HdrPolicies
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

public static class HdrPolicy
{
    public static (bool EmitHdr, bool EmitSdrFallback) Resolve(
        bool sourceIsHdr,
        VideoCodecType codec,
        EncodingProfile baseProfile)
    {
        bool codecSupportsHdr = codec switch
        {
            VideoCodecType.H265 => true,
            VideoCodecType.Av1  => true,
            VideoCodecType.Vp9  => true,
            _ => false
        };

        if (!sourceIsHdr) return (false, false);

        if (codecSupportsHdr)
        {
            return (true, true);
        }

        return (false, true);
    }

    public static bool IsHdrContainerPreferred(Container container) =>
        container switch
        {
            Container.Mkv => true,
            Container.Mp4 => true,
            Container.HlsFmp4 => true,
            _ => false
        };
}