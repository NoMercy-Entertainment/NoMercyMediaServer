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

public record AutoLadderConfig
{
    public LadderTier[] Tiers { get; init; } = LadderTiers.AppleHlsRecommended;
    public BitrateStrategy BitrateStrategy { get; init; } = BitrateStrategy.AppleHlsRecommended;
    public int Crf { get; init; } = 22;
    public double SourcePercentage { get; init; } = 50.0;

    // Default 10 (was 5) so the YouTube ladder's 8 tiers (144p..2160p) survive
    // when JSON deserialization races init-only setters against the C# default.
    // Smaller ladders (Standard 3-rung, Premium 4-rung) are unaffected.
    public int MaxRungs { get; init; } = 10;
    public int MinRungs { get; init; } = 1;
    public bool NeverUpscale { get; init; } = true;
    public bool NeverUpsource { get; init; } = true;
    public double MinTierGapPercent { get; init; } = 50.0;
    public LadderCodecPolicy CodecPolicy { get; init; } = LadderCodecPolicy.Uniform;
    public VideoCodecType? LowTierCodec { get; init; }
    public VideoCodecType? HighTierCodec { get; init; }
    public int MixedPolicySplitHeight { get; init; } = 720;
    public double VbrCeilingMultiplier { get; init; } = 1.5;
    public double BufferSizeMultiplier { get; init; } = 2.0;
    public bool ReduceFramerateForLowTiers { get; init; }
    public double LowTierFramerateMultiplier { get; init; } = 0.5;
    public int LowTierFramerateThresholdHeight { get; init; } = 480;

    /// <summary>
    /// Tier heights at which an extra H.264 8-bit yuv420p fallback rung is
    /// emitted in addition to whatever the codec policy picks. Use this when
    /// a mixed-codec ladder needs a duplicate H.264 variant at a high tier
    /// so HEVC-blocked clients (notably desktop Chrome without an HEVC HW
    /// decoder) can still pull a high-quality stream.
    ///
    /// Example: YouTube profile sets <c>[1080]</c> so 1080p ships as both
    /// HEVC (efficient) AND H.264 (compatible), while 1440p+ stays HEVC-only.
    ///
    /// A height in this list that already resolves to H.264 via the codec
    /// policy is a no-op — no duplicate is emitted. Heights without a
    /// matching tier in <see cref="Tiers"/> are also ignored.
    /// </summary>
    public int[] H264FallbackHeights { get; init; } = [];
}
