namespace NoMercy.Encoder.Hardware;

using NoMercy.Encoder.Codecs;

public record GpuDevice(
    GpuVendor Vendor,
    string Name,
    long VramMb,
    int MaxEncoderSessions,
    IReadOnlyList<VideoCodecType> SupportedCodecs
);
