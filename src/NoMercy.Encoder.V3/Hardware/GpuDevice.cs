namespace NoMercy.Encoder.V3.Hardware;

using NoMercy.Encoder.V3.Codecs;

public record GpuDevice(
    GpuVendor Vendor,
    string Name,
    long VramMb,
    int MaxEncoderSessions,
    IReadOnlyList<VideoCodecType> SupportedCodecs
);
