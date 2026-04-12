namespace NoMercy.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Codecs;

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
