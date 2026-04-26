namespace NoMercy.Encoder.LiveTranscode;

using NoMercy.Encoder.Codecs;

public record ClientCapabilities(
    VideoCodecType[] SupportedVideoCodecs,
    AudioCodecType[] SupportedAudioCodecs,
    string[] SupportedContainers,
    int MaxWidth,
    int MaxHeight,
    bool SupportsHdr,
    bool Supports10Bit,
    int MaxBitrateKbps,
    int MaxAudioChannels = 2
);
