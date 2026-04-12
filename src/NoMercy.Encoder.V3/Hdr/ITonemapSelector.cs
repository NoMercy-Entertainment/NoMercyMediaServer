namespace NoMercy.Encoder.V3.Hdr;

using NoMercy.Encoder.V3.Hardware;

public interface ITonemapSelector
{
    TonemapStrategy SelectBest(IHardwareCapabilities hardware, IFfmpegCapabilities ffmpeg);
}

public record TonemapStrategy(
    TonemapMethod Method,
    string FfmpegFilterChain,
    bool IsGpuAccelerated
);

public enum TonemapMethod
{
    Libplacebo,
    TonemapOpencl,
    ZscaleTonemap,
    CustomLut,
}
