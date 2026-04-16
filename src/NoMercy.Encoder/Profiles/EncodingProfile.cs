namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Codecs;

public record EncodingProfile(
    Ulid Id,
    string Name,
    OutputFormat Format,
    VideoOutput[] VideoOutputs,
    AudioOutput[] AudioOutputs,
    SubtitleOutput[] SubtitleOutputs,
    ThumbnailOutput? Thumbnails = null,
    int SegmentDurationSeconds = 6,
    EncodeMode EncodeMode = EncodeMode.SinglePass,
    int SchemaVersion = 1
);

public record VideoOutput(
    VideoCodecType Codec,
    int Width,
    int? Height,
    int BitrateKbps,
    int Crf,
    string? Preset,
    string? Profile,
    string? Level,
    bool ConvertHdrToSdr,
    int KeyframeIntervalSeconds,
    bool TenBit,
    string SegmentNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
    string PlaylistNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
    string? Tune = null,
    string? ColorSpace = null,
    Dictionary<string, string>? CustomArguments = null
);

public record AudioOutput(
    AudioCodecType Codec,
    int BitrateKbps,
    int Channels,
    int SampleRateHz,
    string[] AllowedLanguages,
    Audio.LoudnessMode Loudness = Audio.LoudnessMode.None,
    string SegmentNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:",
    string PlaylistNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:",
    Dictionary<string, string>? CustomArguments = null
);

public record SubtitleOutput(
    SubtitleCodecType Codec,
    SubtitleMode Mode,
    string[] AllowedLanguages,
    string PlaylistNameTemplate = "subtitles/:filename:.:language:.:variant:",
    Dictionary<string, string>? CustomArguments = null
);

public record ThumbnailOutput(int Width, int IntervalSeconds);
