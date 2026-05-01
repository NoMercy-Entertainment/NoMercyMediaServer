using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Hardware;

public record GpuDevice(
    GpuVendor Vendor,
    string Name,
    long VramMb,
    int MaxEncoderSessions,
    IReadOnlyList<VideoCodecType> SupportedCodecs,
    string? DriverVersion = null
);
