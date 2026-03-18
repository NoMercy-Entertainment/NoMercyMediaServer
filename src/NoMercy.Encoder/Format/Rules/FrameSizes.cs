namespace NoMercy.Encoder.Format.Rules;

public static class FrameSizes
{
    public static readonly Classes.VideoQualityDto _240p = new()
    {
        Name = "240p",
        Width = 426,
        Height = 240
    };

    public static readonly Classes.VideoQualityDto _360p = new()
    {
        Name = "360p",
        Width = 640,
        Height = 360
    };

    public static readonly Classes.VideoQualityDto _480p = new()
    {
        Name = "480p",
        Width = 854,
        Height = 480
    };

    public static readonly Classes.VideoQualityDto _720p = new()
    {
        Name = "720p",
        Width = 1280,
        Height = 720
    };

    public static readonly Classes.VideoQualityDto _1080p = new()
    {
        Name = "1080p",
        Width = 1920,
        Height = 1080
    };

    public static readonly Classes.VideoQualityDto _1440p = new()
    {
        Name = "1440p",
        Width = 2560,
        Height = 1440
    };

    public static readonly Classes.VideoQualityDto _4k = new()
    {
        Name = "4k",
        Width = 3840,
        Height = 2160
    };

    public static readonly Classes.VideoQualityDto _8k = new()
    {
        Name = "8k",
        Width = 7680,
        Height = 4320
    };
}