namespace NoMercy.Encoder.V3.Profiles;

using NoMercy.Encoder.V3.Codecs;

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
    bool TenBit
);
