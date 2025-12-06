namespace NoMercy.NmSystem.Capabilities;

public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
    Qualcomm,
    Apple,
    Unknown
}

/// <summary>
/// Represents a GPU accelerator capability with FFmpeg arguments
/// </summary>
public class GpuAccelerator
{
    public GpuVendor Vendor { get; }
    public string FfmpegArgs { get; }
    public string Filter { get; }
    public string Accelerator { get; }

    public GpuAccelerator(GpuVendor vendor, string ffmpegArgs, string accelerator, string filter = "")
    {
        Vendor = vendor;
        FfmpegArgs = ffmpegArgs;
        Filter = filter;
        Accelerator = accelerator;
    }
}
