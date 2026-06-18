namespace NoMercy.Encoder.Hardware;

/// <summary>
/// The hardware-accelerated decode + scale combination for a GPU-resident encode:
/// frames are decoded into GPU memory (<see cref="HwAccelDevice"/> /
/// <see cref="HwAccelOutputFormat"/>) and scaled there (<see cref="ScaleFilter"/>)
/// before the encoder reads them, so decode + scaling leave the CPU.
/// </summary>
public record GpuAccelPlan(string HwAccelDevice, string HwAccelOutputFormat, string ScaleFilter);

/// <summary>
/// Maps a detected GPU vendor to its decode-accel + GPU-scaler combination, gated
/// on the scaler actually being present in the running ffmpeg build. Returns null
/// for "no GPU-resident path — use the CPU filter graph", which is the safe
/// default for AMD (no scale_amf in this build), Apple (cross-platform future),
/// missing filters, or no GPU at all.
/// </summary>
public static class GpuAccelResolver
{
    public static GpuAccelPlan? Resolve(GpuVendor? vendor, Func<string, bool> hasFilter)
    {
        return vendor switch
        {
            GpuVendor.Nvidia when hasFilter("scale_cuda") => new("cuda", "cuda", "scale_cuda"),
            GpuVendor.Intel when hasFilter("scale_qsv") => new("qsv", "qsv", "scale_qsv"),
            _ => null,
        };
    }
}
