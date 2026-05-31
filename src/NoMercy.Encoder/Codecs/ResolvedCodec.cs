using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Codecs;

public record ResolvedCodec(
    string FfmpegEncoderName,
    EncoderInfo EncoderInfo,
    GpuDevice? Device,
    RateControlMode DefaultRateControl
);
