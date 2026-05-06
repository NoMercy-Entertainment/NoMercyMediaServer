using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public record VideoOutput(
    StreamPolicy Policy,
    VideoCodecType Codec,
    int Width,
    int? Height,
    RateControlMode RateControl,
    int Crf,
    int BitrateKbps,
    int? MaxBitrateKbps,
    int? BufferSizeKbps,
    string? Preset,
    CodecProfile CodecProfile,
    string? Level,
    string? Tune,
    int BitDepth,
    string? PixelFormat,
    int KeyframeIntervalSeconds,
    bool ConvertHdrToSdr,
    string SegmentNameTemplate,
    string PlaylistNameTemplate,
    Dictionary<string, string>? CustomArguments = null
);
