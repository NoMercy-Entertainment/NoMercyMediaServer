namespace NoMercy.Plugins.Abstractions;

public class EncodingProfile
{
    public required string Name { get; init; }
    public required string VideoCodec { get; init; }
    public required string AudioCodec { get; init; }
    public string Container { get; init; } = "mp4";
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? VideoBitrate { get; init; }
    public int? AudioBitrate { get; init; }
    public Dictionary<string, string> ExtraParameters { get; init; } = new();
}
