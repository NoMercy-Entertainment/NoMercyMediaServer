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

public static class LadderTiers
{
    // VP9 ≈ 0.65× H.264; AV1 ≈ 0.50× H.264 (AOMedia/Netflix benchmarks).
    // HEVC ≈ 0.60× H.264 (Apple HLS spec and encoder guidance).

    public static readonly LadderTier[] AppleHlsRecommended =
    [
        new(
            640,
            360,
            "360p",
            RecommendedBitrateH264Kbps: 365,
            RecommendedBitrateHevcKbps: 200,
            RecommendedBitrateAv1Kbps: 150,
            RecommendedBitrateVp9Kbps: 237
        ),
        new(
            960,
            540,
            "540p",
            RecommendedBitrateH264Kbps: 2000,
            RecommendedBitrateHevcKbps: 800,
            RecommendedBitrateAv1Kbps: 600,
            RecommendedBitrateVp9Kbps: 1300
        ),
        new(
            1280,
            720,
            "720p",
            RecommendedBitrateH264Kbps: 3000,
            RecommendedBitrateHevcKbps: 1600,
            RecommendedBitrateAv1Kbps: 1200,
            RecommendedBitrateVp9Kbps: 1950
        ),
        new(
            1920,
            1080,
            "1080p",
            RecommendedBitrateH264Kbps: 6000,
            RecommendedBitrateHevcKbps: 3400,
            RecommendedBitrateAv1Kbps: 2500,
            RecommendedBitrateVp9Kbps: 3900
        ),
        new(
            2560,
            1440,
            "1440p",
            RecommendedBitrateH264Kbps: 12000,
            RecommendedBitrateHevcKbps: 6000,
            RecommendedBitrateAv1Kbps: 4500,
            RecommendedBitrateVp9Kbps: 7800
        ),
        new(
            3840,
            2160,
            "2160p",
            RecommendedBitrateH264Kbps: 24000,
            RecommendedBitrateHevcKbps: 11600,
            RecommendedBitrateAv1Kbps: 8000,
            RecommendedBitrateVp9Kbps: 15600
        ),
    ];

    public static readonly LadderTier[] Standard =
    [
        new(
            854,
            480,
            "480p",
            RecommendedBitrateH264Kbps: 1500,
            RecommendedBitrateHevcKbps: null,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            1280,
            720,
            "720p",
            RecommendedBitrateH264Kbps: 3000,
            RecommendedBitrateHevcKbps: null,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            1920,
            1080,
            "1080p",
            RecommendedBitrateH264Kbps: 6000,
            RecommendedBitrateHevcKbps: null,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
    ];
    public static readonly LadderTier[] YouTube =
    [
        new(256, 144, "144p", 80, 80, 60, 60),
        new(426, 240, "240p", 150, 150, 120, 120),
        new(640, 360, "360p", 400, 350, 300, 300),
        new(854, 480, "480p", 1000, 900, 800, 800),
        new(1280, 720, "720p", 2500, 2000, 1800, 1800),
        new(1920, 1080, "1080p", 4500, 3500, 3000, 3000),
        new(2560, 1440, "1440p", 10000, 8000, 7000, 7000),
        new(3840, 2160, "2160p", 20000, 16000, 14000, 14000)
    ];

    public static readonly LadderTier[] Premium =
    [
        new(640, 360, "360p", 600, 500, 450, 450),
        new(854, 480, "480p", 1500, 1200, 1000, 1000),
        new(1280, 720, "720p", 3500, 3000, 2500, 2500),
        new(1920, 1080, "1080p", 7000, 6000, 5000, 5000),
        new(2560, 1440, "1440p", 14000, 12000, 10000, 10000),
        new(3840, 2160, "2160p", 30000, 25000, 22000, 22000)
    ];
}
