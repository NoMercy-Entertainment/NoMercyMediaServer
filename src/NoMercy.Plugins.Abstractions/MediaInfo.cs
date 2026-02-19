namespace NoMercy.Plugins.Abstractions;

public class MediaInfo
{
    public required string FilePath { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public long? Bitrate { get; init; }
    public TimeSpan? Duration { get; init; }
    public bool IsHdr { get; init; }
}
