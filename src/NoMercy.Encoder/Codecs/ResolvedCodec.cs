namespace NoMercy.Encoder.Codecs;

using NoMercy.Encoder.Hardware;

public record ResolvedCodec(
    string FfmpegEncoderName,
    EncoderInfo EncoderInfo,
    GpuDevice? Device,
    RateControlMode DefaultRateControl
);
