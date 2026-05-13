namespace NoMercy.Encoder.Profiles;

public static class LadderTiers
{
    public static readonly LadderTier[] AppleHlsRecommended =
    [
        new(640, 360, "360p", 365, 200, 150),
        new(960, 540, "540p", 2000, 800, 600),
        new(1280, 720, "720p", 3000, 1600, 1200),
        new(1920, 1080, "1080p", 6000, 3400, 2500),
        new(2560, 1440, "1440p", 12000, 6000, 4500),
        new(3840, 2160, "2160p", 24000, 11600, 8000),
    ];

    public static readonly LadderTier[] Standard =
    [
        new(854, 480, "480p", 1500, null, null),
        new(1280, 720, "720p", 3000, null, null),
        new(1920, 1080, "1080p", 6000, null, null),
    ];

    public static readonly LadderTier[] Premium =
    [
        new(854, 480, "480p", 1500, 1000, null),
        new(1280, 720, "720p", 3000, 1600, null),
        new(1920, 1080, "1080p", 6000, 3400, null),
        new(3840, 2160, "2160p", 24000, 11600, null),
    ];

    public static readonly LadderTier[] Mobile =
    [
        new(640, 360, "360p", 365, 200, null),
        new(854, 480, "480p", 1100, 600, null),
        new(1280, 720, "720p", 2000, 1200, null),
    ];

    // YouTube SDR/HDR upload-style bitrate guidance for the full 144p → 2160p range.
    // H.264 numbers track YouTube's "Recommended upload bitrate" table; HEVC is set
    // around 60% of H.264 (HDR sources go HEVC for passthrough); AV1 around 40%.
    public static readonly LadderTier[] YouTube =
    [
        new(256, 144, "144p", 80, 60, 40),
        new(426, 240, "240p", 300, 200, 150),
        new(640, 360, "360p", 700, 500, 350),
        new(854, 480, "480p", 1500, 1000, 750),
        new(1280, 720, "720p", 4000, 2500, 1800),
        new(1920, 1080, "1080p", 8000, 5000, 3500),
        new(2560, 1440, "1440p", 16000, 9000, 6500),
        new(3840, 2160, "2160p", 35000, 18000, 12000),
    ];
}
