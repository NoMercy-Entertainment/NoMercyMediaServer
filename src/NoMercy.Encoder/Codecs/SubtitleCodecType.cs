namespace NoMercy.Encoder.Codecs;

public enum SubtitleCodecType
{
    WebVtt,
    Srt,
    Ass,
    Pgs,

    /// <summary>
    /// Subtitle stream copy (no transcode). Preserves PGS / DVB image
    /// subtitles, ASS typesetting, and SRT timing exactly as the source
    /// holds them — useful for archival MKV outputs where conversion to
    /// WebVTT would lose typesetting or image-sub fidelity. Encoder emits
    /// <c>-c:s copy</c>.
    /// </summary>
    Copy,
}
