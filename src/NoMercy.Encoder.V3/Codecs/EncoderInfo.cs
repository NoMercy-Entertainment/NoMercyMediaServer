namespace NoMercy.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Hardware;

public record EncoderInfo(
    string FfmpegName,
    GpuVendor? RequiredVendor,
    string[] Presets,
    string[] Profiles,
    string[] Levels,
    QualityRange QualityRange,
    RateControlMode[] SupportedRateControl,
    bool Supports10Bit,
    bool SupportsHdr,
    int MaxConcurrentSessions,
    string PixelFormat10Bit,
    Dictionary<string, string> VendorSpecificFlags
);
