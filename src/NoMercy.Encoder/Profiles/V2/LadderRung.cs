using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles.V2;

public record LadderRung(
    int Width,
    int Height,
    VideoCodecType Codec,
    int BitrateKbps,
    int MaxBitrateKbps,
    int BufferSizeKbps,
    double Framerate,
    string? Preset = null,
    CodecProfile CodecProfile = CodecProfile.Auto,
    int BitDepth = 8,
    string? PixelFormat = null
);
