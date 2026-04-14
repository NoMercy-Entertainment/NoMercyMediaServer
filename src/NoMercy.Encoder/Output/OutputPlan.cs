namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;

public record OutputPlan(
    OutputFormat Format,
    VideoOutputPlan[] VideoOutputs,
    AudioOutputPlan[] AudioOutputs,
    SubtitleOutputPlan[] SubtitleOutputs,
    ThumbnailOutputPlan? Thumbnails
);

public record VideoOutputPlan(
    int Width,
    int Height,
    string EncoderName,
    int Crf,
    int BitrateKbps,
    string? Preset,
    string? Profile,
    string? Level,
    bool TenBit,
    string PixelFormat,
    string MapLabel,
    Dictionary<string, string> ExtraFlags,
    double FrameRate = 23.976,
    string SegmentNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
    string PlaylistNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
);

public record AudioOutputPlan(
    string EncoderName,
    int BitrateKbps,
    int Channels,
    int SampleRate,
    StreamAction Action,
    string? Language,
    string MapLabel,
    string SegmentNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:",
    string PlaylistNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:"
);

public record SubtitleOutputPlan(
    SubtitleCodecType OutputCodec,
    StreamAction Action,
    string? Language,
    int SourceIndex,
    string? MapLabel,
    string PlaylistNameTemplate = "subtitles/:filename:.:language:.:variant:"
);

public record ThumbnailOutputPlan(int Width, int IntervalSeconds);
