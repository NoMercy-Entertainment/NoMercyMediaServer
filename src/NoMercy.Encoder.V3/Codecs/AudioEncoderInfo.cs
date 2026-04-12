namespace NoMercy.Encoder.V3.Codecs;

public record AudioEncoderInfo(
    string FfmpegName,
    AudioCodecType CodecType,
    int[] Channels,
    int[] SampleRates,
    int MinBitrateKbps,
    int MaxBitrateKbps,
    int DefaultBitrateKbps,
    bool IsLossless
);
