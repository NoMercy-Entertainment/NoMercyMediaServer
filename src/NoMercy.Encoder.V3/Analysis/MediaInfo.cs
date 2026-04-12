namespace NoMercy.Encoder.V3.Analysis;

public record MediaInfo(
    string FilePath,
    string Format,
    TimeSpan Duration,
    long OverallBitRateKbps,
    long FileSizeBytes,
    IReadOnlyList<VideoStreamInfo> VideoStreams,
    IReadOnlyList<AudioStreamInfo> AudioStreams,
    IReadOnlyList<SubtitleStreamInfo> SubtitleStreams,
    IReadOnlyList<ChapterInfo> Chapters
)
{
    public bool HasVideo => VideoStreams.Count > 0;
    public bool HasAudio => AudioStreams.Count > 0;
    public bool HasSubtitles => SubtitleStreams.Count > 0;
}
