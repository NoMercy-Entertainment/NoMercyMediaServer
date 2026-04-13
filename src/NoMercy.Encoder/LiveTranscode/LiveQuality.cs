namespace NoMercy.Encoder.LiveTranscode;

using NoMercy.Encoder.Codecs;

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
