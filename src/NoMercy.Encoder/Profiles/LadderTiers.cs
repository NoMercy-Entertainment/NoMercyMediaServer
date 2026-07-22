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
            Width: 640,
            Height: 360,
            Label: "360p",
            RecommendedBitrateH264Kbps: 365,
            RecommendedBitrateHevcKbps: 200,
            RecommendedBitrateAv1Kbps: 150,
            RecommendedBitrateVp9Kbps: 237
        ),
        new(
            Width: 960,
            Height: 540,
            Label: "540p",
            RecommendedBitrateH264Kbps: 2000,
            RecommendedBitrateHevcKbps: 800,
            RecommendedBitrateAv1Kbps: 600,
            RecommendedBitrateVp9Kbps: 1300
        ),
        new(
            Width: 1280,
            Height: 720,
            Label: "720p",
            RecommendedBitrateH264Kbps: 3000,
            RecommendedBitrateHevcKbps: 1600,
            RecommendedBitrateAv1Kbps: 1200,
            RecommendedBitrateVp9Kbps: 1950
        ),
        new(
            Width: 1920,
            Height: 1080,
            Label: "1080p",
            RecommendedBitrateH264Kbps: 6000,
            RecommendedBitrateHevcKbps: 3400,
            RecommendedBitrateAv1Kbps: 2500,
            RecommendedBitrateVp9Kbps: 3900
        ),
        new(
            Width: 2560,
            Height: 1440,
            Label: "1440p",
            RecommendedBitrateH264Kbps: 12000,
            RecommendedBitrateHevcKbps: 6000,
            RecommendedBitrateAv1Kbps: 4500,
            RecommendedBitrateVp9Kbps: 7800
        ),
        new(
            Width: 3840,
            Height: 2160,
            Label: "2160p",
            RecommendedBitrateH264Kbps: 24000,
            RecommendedBitrateHevcKbps: 11600,
            RecommendedBitrateAv1Kbps: 8000,
            RecommendedBitrateVp9Kbps: 15600
        ),
    ];

    public static readonly LadderTier[] Standard =
    [
        new(
            Width: 854,
            Height: 480,
            Label: "480p",
            RecommendedBitrateH264Kbps: 1500,
            RecommendedBitrateHevcKbps: null,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            Width: 1280,
            Height: 720,
            Label: "720p",
            RecommendedBitrateH264Kbps: 3000,
            RecommendedBitrateHevcKbps: null,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            Width: 1920,
            Height: 1080,
            Label: "1080p",
            RecommendedBitrateH264Kbps: 6000,
            RecommendedBitrateHevcKbps: null,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
    ];
    public static readonly LadderTier[] YouTube =
    [
        new(Width: 256, Height: 144, Label: "144p", RecommendedBitrateH264Kbps: 80, RecommendedBitrateHevcKbps: 80, RecommendedBitrateAv1Kbps: 60, RecommendedBitrateVp9Kbps: 60),
        new(Width: 426, Height: 240, Label: "240p", RecommendedBitrateH264Kbps: 150, RecommendedBitrateHevcKbps: 150, RecommendedBitrateAv1Kbps: 120, RecommendedBitrateVp9Kbps: 120),
        new(Width: 640, Height: 360, Label: "360p", RecommendedBitrateH264Kbps: 400, RecommendedBitrateHevcKbps: 350, RecommendedBitrateAv1Kbps: 300, RecommendedBitrateVp9Kbps: 300),
        new(Width: 854, Height: 480, Label: "480p", RecommendedBitrateH264Kbps: 1000, RecommendedBitrateHevcKbps: 900, RecommendedBitrateAv1Kbps: 800, RecommendedBitrateVp9Kbps: 800),
        new(Width: 1280, Height: 720, Label: "720p", RecommendedBitrateH264Kbps: 2500, RecommendedBitrateHevcKbps: 2000, RecommendedBitrateAv1Kbps: 1800, RecommendedBitrateVp9Kbps: 1800),
        new(Width: 1920, Height: 1080, Label: "1080p", RecommendedBitrateH264Kbps: 4500, RecommendedBitrateHevcKbps: 3500, RecommendedBitrateAv1Kbps: 3000, RecommendedBitrateVp9Kbps: 3000),
        new(Width: 2560, Height: 1440, Label: "1440p", RecommendedBitrateH264Kbps: 10000, RecommendedBitrateHevcKbps: 8000, RecommendedBitrateAv1Kbps: 7000, RecommendedBitrateVp9Kbps: 7000),
        new(Width: 3840, Height: 2160, Label: "2160p", RecommendedBitrateH264Kbps: 20000, RecommendedBitrateHevcKbps: 16000, RecommendedBitrateAv1Kbps: 14000, RecommendedBitrateVp9Kbps: 14000)
    ];

    public static readonly LadderTier[] Premium =
    [
        new(Width: 640, Height: 360, Label: "360p", RecommendedBitrateH264Kbps: 600, RecommendedBitrateHevcKbps: 500, RecommendedBitrateAv1Kbps: 450, RecommendedBitrateVp9Kbps: 450),
        new(Width: 854, Height: 480, Label: "480p", RecommendedBitrateH264Kbps: 1500, RecommendedBitrateHevcKbps: 1200, RecommendedBitrateAv1Kbps: 1000, RecommendedBitrateVp9Kbps: 1000),
        new(Width: 1280, Height: 720, Label: "720p", RecommendedBitrateH264Kbps: 3500, RecommendedBitrateHevcKbps: 3000, RecommendedBitrateAv1Kbps: 2500, RecommendedBitrateVp9Kbps: 2500),
        new(Width: 1920, Height: 1080, Label: "1080p", RecommendedBitrateH264Kbps: 7000, RecommendedBitrateHevcKbps: 6000, RecommendedBitrateAv1Kbps: 5000, RecommendedBitrateVp9Kbps: 5000),
        new(Width: 2560, Height: 1440, Label: "1440p", RecommendedBitrateH264Kbps: 14000, RecommendedBitrateHevcKbps: 12000, RecommendedBitrateAv1Kbps: 10000, RecommendedBitrateVp9Kbps: 10000),
        new(Width: 3840, Height: 2160, Label: "2160p", RecommendedBitrateH264Kbps: 30000, RecommendedBitrateHevcKbps: 25000, RecommendedBitrateAv1Kbps: 22000, RecommendedBitrateVp9Kbps: 22000)
    ];
}
