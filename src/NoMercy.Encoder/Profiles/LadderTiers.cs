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

    public static readonly LadderTier[] Premium =
    [
        new(
            854,
            480,
            "480p",
            RecommendedBitrateH264Kbps: 1500,
            RecommendedBitrateHevcKbps: 1000,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            1280,
            720,
            "720p",
            RecommendedBitrateH264Kbps: 3000,
            RecommendedBitrateHevcKbps: 1600,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            1920,
            1080,
            "1080p",
            RecommendedBitrateH264Kbps: 6000,
            RecommendedBitrateHevcKbps: 3400,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            3840,
            2160,
            "2160p",
            RecommendedBitrateH264Kbps: 24000,
            RecommendedBitrateHevcKbps: 11600,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
    ];

    public static readonly LadderTier[] Mobile =
    [
        new(
            640,
            360,
            "360p",
            RecommendedBitrateH264Kbps: 365,
            RecommendedBitrateHevcKbps: 200,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            854,
            480,
            "480p",
            RecommendedBitrateH264Kbps: 1100,
            RecommendedBitrateHevcKbps: 600,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
        new(
            1280,
            720,
            "720p",
            RecommendedBitrateH264Kbps: 2000,
            RecommendedBitrateHevcKbps: 1200,
            RecommendedBitrateAv1Kbps: null,
            RecommendedBitrateVp9Kbps: null
        ),
    ];

    // YouTube SDR/HDR upload-style bitrate guidance for the full 144p → 2160p range.
    // H.264 numbers track YouTube's "Recommended upload bitrate" table.
    public static readonly LadderTier[] YouTube =
    [
        new(
            256,
            144,
            "144p",
            RecommendedBitrateH264Kbps: 80,
            RecommendedBitrateHevcKbps: 60,
            RecommendedBitrateAv1Kbps: 40,
            RecommendedBitrateVp9Kbps: 52
        ),
        new(
            426,
            240,
            "240p",
            RecommendedBitrateH264Kbps: 300,
            RecommendedBitrateHevcKbps: 200,
            RecommendedBitrateAv1Kbps: 150,
            RecommendedBitrateVp9Kbps: 195
        ),
        new(
            640,
            360,
            "360p",
            RecommendedBitrateH264Kbps: 700,
            RecommendedBitrateHevcKbps: 500,
            RecommendedBitrateAv1Kbps: 350,
            RecommendedBitrateVp9Kbps: 455
        ),
        new(
            854,
            480,
            "480p",
            RecommendedBitrateH264Kbps: 1500,
            RecommendedBitrateHevcKbps: 1000,
            RecommendedBitrateAv1Kbps: 750,
            RecommendedBitrateVp9Kbps: 975
        ),
        new(
            1280,
            720,
            "720p",
            RecommendedBitrateH264Kbps: 4000,
            RecommendedBitrateHevcKbps: 2500,
            RecommendedBitrateAv1Kbps: 1800,
            RecommendedBitrateVp9Kbps: 2600
        ),
        new(
            1920,
            1080,
            "1080p",
            RecommendedBitrateH264Kbps: 8000,
            RecommendedBitrateHevcKbps: 5000,
            RecommendedBitrateAv1Kbps: 3500,
            RecommendedBitrateVp9Kbps: 5200
        ),
        new(
            2560,
            1440,
            "1440p",
            RecommendedBitrateH264Kbps: 16000,
            RecommendedBitrateHevcKbps: 9000,
            RecommendedBitrateAv1Kbps: 6500,
            RecommendedBitrateVp9Kbps: 10400
        ),
        new(
            3840,
            2160,
            "2160p",
            RecommendedBitrateH264Kbps: 35000,
            RecommendedBitrateHevcKbps: 18000,
            RecommendedBitrateAv1Kbps: 12000,
            RecommendedBitrateVp9Kbps: 22750
        ),
    ];
}
