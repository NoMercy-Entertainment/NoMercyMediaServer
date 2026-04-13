namespace NoMercy.Encoder.Hdr;

using NoMercy.Encoder.Hardware;

public interface ITonemapSelector
{
    TonemapStrategy SelectBest(IHardwareCapabilities hardware, IFfmpegCapabilities? ffmpeg = null);
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
