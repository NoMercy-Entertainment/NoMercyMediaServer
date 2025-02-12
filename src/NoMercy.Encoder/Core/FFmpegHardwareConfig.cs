using System.Runtime.InteropServices;
using NoMercy.NmSystem;

namespace NoMercy.Encoder.Core;

public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
    Qualcomm,
    Apple,
    Unknown
}

public class FFmpegHardwareConfig
{
    public List<GpuAccelerator> Accelerators { get; private set; } = new();
    
    public FFmpegHardwareConfig()
    {
        SetHardwareAccelerationFlags(Info.GpuVendors);
    }
    
    public bool HasAccelerator(string accelerator)
    {
        return Accelerators.Any(a => a.Accelerator == accelerator);
    }
    
    private void SetHardwareAccelerationFlags(List<string> gpuVendors)
    {
        Dictionary<GpuVendor, int> gpuCounts = new()
        {
            { GpuVendor.Nvidia, 0 },
            { GpuVendor.Amd, 0 },
            { GpuVendor.Intel, 0 },
            { GpuVendor.Qualcomm, 0 },
            { GpuVendor.Apple, 0 }
        };

        foreach (string vendor in gpuVendors.Select(v => v.ToLower()))
        {
            if (vendor.Contains("nvidia"))
            {
                int index = gpuCounts[GpuVendor.Nvidia];
                Accelerators.Add(new(
                    vendor: GpuVendor.Nvidia,
                    ffmpegArgs: $"-hwaccel cuda -init_hw_device cuda=cu:{index} -filter_hw_device cu -hwaccel_output_format cuda",
                    accelerator: "cuda"
                ));
                gpuCounts[GpuVendor.Nvidia]++;
            }
            else if (vendor.Contains("amd") || vendor.Contains("advanced micro devices"))
            {
                int index = gpuCounts[GpuVendor.Amd];
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Accelerators.Add(new(
                        vendor: GpuVendor.Amd,
                        ffmpegArgs: $"-hwaccel dxva2 -init_hw_device dxva2=hw{index} -filter_hw_device hw -hwaccel_output_format dxva2",
                        accelerator: "dxva2"
                    ));
                }
                else
                {
                    Accelerators.Add(new(
                        vendor: GpuVendor.Amd,
                        ffmpegArgs: $"-hwaccel vaapi -init_hw_device vaapi=hw{index}:/dev/dri/renderD128 -filter_hw_device hw -hwaccel_output_format vaapi",
                        filter: "hwupload",
                        accelerator: "vaapi"
                    ));
                }
                gpuCounts[GpuVendor.Amd]++;
            }
            else if (vendor.Contains("intel"))
            {
                int index = gpuCounts[GpuVendor.Intel];
                Accelerators.Add(new(
                    vendor: GpuVendor.Intel,
                    ffmpegArgs: $"-hwaccel qsv -init_hw_device qsv=hw{index} -filter_hw_device hw -hwaccel_output_format qsv",
                    accelerator: "qsv"
                ));
                gpuCounts[GpuVendor.Intel]++;
            }
            else if (vendor.Contains("qualcomm"))
            {
                int index = gpuCounts[GpuVendor.Qualcomm];
                Accelerators.Add(new(
                    vendor: GpuVendor.Qualcomm,
                    ffmpegArgs: $"-hwaccel opencl -init_hw_device opencl=hw{index} -filter_hw_device hw",
                    accelerator: "opencl"
                ));
                gpuCounts[GpuVendor.Qualcomm]++;
            }
            else if (vendor.Contains("apple"))
            {
                int index = gpuCounts[GpuVendor.Apple];
                Accelerators.Add(new(
                    vendor: GpuVendor.Apple,
                    ffmpegArgs: $"-hwaccel videotoolbox -init_hw_device videotoolbox:hw{index} -filter_hw_device hw",
                    accelerator: "videotoolbox"
                ));
                gpuCounts[GpuVendor.Apple]++;
            }
            else
            {
                Accelerators.Add(new(
                    vendor: GpuVendor.Unknown,
                    ffmpegArgs: "-extra_hw_frames 3 -init_hw_device opencl=ocl",
                    accelerator: "none"
                ));
            }
        }
    }
}

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