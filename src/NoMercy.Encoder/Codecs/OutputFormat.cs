namespace NoMercy.Encoder.Codecs;

public enum OutputFormat
{
    Hls,
    Mkv,
    Mp4,
    Dash,

    // Audio-only single-file containers. Music collectors want raw archival
    // outputs; these produce a single {title}.{ext} file with no sidecars.
    Mp3,
    Flac,
    Ogg,
}
