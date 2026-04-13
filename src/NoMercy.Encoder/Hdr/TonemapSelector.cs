namespace NoMercy.Encoder.Hdr;

using NoMercy.Encoder.Hardware;

public class TonemapSelector : ITonemapSelector
{
    public TonemapStrategy SelectBest(
        IHardwareCapabilities hardware,
        IFfmpegCapabilities? ffmpeg = null
    )
    {
        // Priority: libplacebo (Vulkan GPU) → tonemap_opencl (OpenCL) → zscale+tonemap (CPU)
        if (ffmpeg is not null && ffmpeg.HasFilter("libplacebo"))
            return new TonemapStrategy(
                TonemapMethod.Libplacebo,
                "libplacebo=tonemapping=hable:color_primaries=bt709:color_trc=bt709:colorspace=bt709:format=yuv420p",
                true
            );

        if (ffmpeg is not null && ffmpeg.HasFilter("tonemap_opencl"))
            return new TonemapStrategy(
                TonemapMethod.TonemapOpencl,
                "tonemap_opencl=tonemap=hable:desat=0:format=nv12",
                true
            );

        return new TonemapStrategy(
            TonemapMethod.ZscaleTonemap,
            "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p",
            false
        );
    }
}
