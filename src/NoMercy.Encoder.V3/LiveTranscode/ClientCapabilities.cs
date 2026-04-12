namespace NoMercy.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Codecs;

public record ClientCapabilities(
    VideoCodecType[] SupportedVideoCodecs,
    AudioCodecType[] SupportedAudioCodecs,
    string[] SupportedContainers,
    int MaxWidth,
    int MaxHeight,
    bool SupportsHdr,
    bool Supports10Bit,
    int MaxBitrateKbps
);
