namespace NoMercy.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Hardware;

public record ResolvedCodec(
    string FfmpegEncoderName,
    EncoderInfo EncoderInfo,
    GpuDevice? Device,
    RateControlMode DefaultRateControl
);
