namespace NoMercy.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Pipeline;

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
    Dictionary<string, string> ExtraFlags
);

public record AudioOutputPlan(
    string EncoderName,
    int BitrateKbps,
    int Channels,
    int SampleRate,
    StreamAction Action,
    string? Language,
    string MapLabel
);

public record SubtitleOutputPlan(
    SubtitleCodecType OutputCodec,
    StreamAction Action,
    string? Language,
    int SourceIndex,
    string? MapLabel
);

public record ThumbnailOutputPlan(int Width, int IntervalSeconds);
