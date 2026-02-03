namespace NoMercy.EncoderV2.Abstractions;

/// <summary>
/// Represents analyzed media information
/// </summary>
public sealed class MediaInfo
{
    public string FilePath { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public long FileSize { get; init; }
    public string? Format { get; init; }
    public long? Bitrate { get; init; }

    public IReadOnlyList<VideoStreamInfo> VideoStreams { get; init; } = [];
    public IReadOnlyList<AudioStreamInfo> AudioStreams { get; init; } = [];
    public IReadOnlyList<SubtitleStreamInfo> SubtitleStreams { get; init; } = [];
    public IReadOnlyList<ChapterInfo> Chapters { get; init; } = [];
}

public sealed class VideoStreamInfo
{
    public int Index { get; init; }
    public string? Codec { get; init; }
    public string? Profile { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double FrameRate { get; init; }
    public long? Bitrate { get; init; }
    public string? PixelFormat { get; init; }
    public string? ColorSpace { get; init; }
    public string? ColorTransfer { get; init; }
    public string? ColorPrimaries { get; init; }
    public bool IsHdr { get; init; }
    public bool IsInterlaced { get; init; }
    public string? Language { get; init; }
    public bool IsDefault { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class AudioStreamInfo
{
    public int Index { get; init; }
    public string? Codec { get; init; }
    public string? Profile { get; init; }
    public int Channels { get; init; }
    public string? ChannelLayout { get; init; }
    public int SampleRate { get; init; }
    public long? Bitrate { get; init; }
    public string? Language { get; init; }
    public string? Title { get; init; }
    public bool IsDefault { get; init; }
    public bool IsForced { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class SubtitleStreamInfo
{
    public int Index { get; init; }
    public string? Codec { get; init; }
    public string? Language { get; init; }
    public string? Title { get; init; }
    public bool IsDefault { get; init; }
    public bool IsForced { get; init; }
    public bool IsHearingImpaired { get; init; }
}

public sealed class ChapterInfo
{
    public int Index { get; init; }
    public string? Title { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
}

/// <summary>
/// Abstraction for media file analysis using FFprobe
/// </summary>
public interface IMediaAnalyzer
{
    /// <summary>
    /// Analyzes a media file and returns detailed information
    /// </summary>
    Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets just the duration of a media file (faster than full analysis)
    /// </summary>
    Task<TimeSpan> GetDurationAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file is a valid media file
    /// </summary>
    Task<bool> IsValidMediaFileAsync(string filePath, CancellationToken cancellationToken = default);
}
