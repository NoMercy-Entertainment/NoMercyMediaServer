using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.LiveTranscode;

public record LiveQuality(
    string Id,
    string Label,
    int Width,
    int Height,
    VideoCodecType Codec,
    int BitrateKbps,
    string Encoder,
    bool IsHardwareAccelerated,
    double ExpectedSpeed,
    bool CanRealtime
);
