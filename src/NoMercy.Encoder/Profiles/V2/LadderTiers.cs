namespace NoMercy.Encoder.Profiles.V2;

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
}
