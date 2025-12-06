using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.NmSystem.Capabilities;

/// <summary>
/// Detects GPU devices using proper system tools (nvidia-smi, rocm-smi, intel_gpu_top)
/// </summary>
public class GpuDeviceDetector
{
    /// <summary>
    /// Detect all GPU devices on the system
    /// </summary>
    public List<GpuDevice> DetectGpuDevices()
    {
        List<GpuDevice> devices = new();

        try
        {
            Logger.App("Detecting GPU devices using system tools (nvidia-smi, rocm-smi, intel_gpu_top)...");

            // Detect NVIDIA CUDA devices
            devices.AddRange(DetectNvidiaDevices());

            // Detect AMD devices (via rocm-smi)
            devices.AddRange(DetectAmdDevices());

            // Detect Intel Arc devices
            devices.AddRange(DetectIntelDevices());

            if (devices.Count > 0)
            {
                Logger.App($"GPU detection complete. Found {devices.Count} device(s):", Serilog.Events.LogEventLevel.Information);
                foreach (var device in devices)
                {
                    Logger.App($"  - {device.DeviceName} ({device.DeviceType}): {device.MemoryMb}MB, Compute: {device.ComputeCapability}, Encoders: {string.Join(", ", device.SupportedEncoders)}", Serilog.Events.LogEventLevel.Information);
                }
            }
            else
            {
                Logger.App("No GPU devices detected on this system");
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to detect GPU devices: {ex.Message}", Serilog.Events.LogEventLevel.Error);
        }

        return devices;
    }

    /// <summary>
    /// Detect NVIDIA CUDA devices using nvidia-smi
    /// </summary>
    private List<GpuDevice> DetectNvidiaDevices()
    {
        List<GpuDevice> devices = new();

        try
        {
            string nvidiaSmiPath = FindExecutableInPath("nvidia-smi");
            
            if (string.IsNullOrEmpty(nvidiaSmiPath))
            {
                Logger.App("nvidia-smi not found in system PATH", Serilog.Events.LogEventLevel.Debug);
                return devices;
            }

            ProcessStartInfo processInfo = new()
            {
                FileName = nvidiaSmiPath,
                Arguments = "--query-gpu=index,name,memory.total,compute_cap --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(processInfo);
            if (process == null) return devices;

            using StreamReader reader = process.StandardOutput;
            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split(',').Select(p => p.Trim()).ToArray();
                if (parts.Length < 4) continue;

                if (int.TryParse(parts[0], out int deviceId) && long.TryParse(parts[2], out long memoryMb))
                {
                    GpuDevice device = new()
                    {
                        DeviceId = deviceId,
                        DeviceName = parts[1],
                        DeviceType = "NVIDIA",
                        MemoryMb = memoryMb,
                        ComputeCapability = parts[3],
                        SupportedEncoders = new() { "h264_nvenc", "hevc_nvenc", "av1_nvenc" }
                    };

                    devices.Add(device);
                    Logger.App($"Detected NVIDIA GPU: {device.DeviceName} ({memoryMb}MB)", Serilog.Events.LogEventLevel.Information);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to detect NVIDIA GPUs via nvidia-smi: {ex.Message}", Serilog.Events.LogEventLevel.Warning);
        }

        return devices;
    }

    /// <summary>
    /// Detect AMD GPUs using rocm-smi
    /// </summary>
    private List<GpuDevice> DetectAmdDevices()
    {
        List<GpuDevice> devices = new();

        try
        {
            string rocmSmiPath = FindExecutableInPath("rocm-smi");
            
            if (string.IsNullOrEmpty(rocmSmiPath))
            {
                Logger.App("rocm-smi not found in system PATH", Serilog.Events.LogEventLevel.Debug);
                return devices;
            }

            ProcessStartInfo processInfo = new()
            {
                FileName = rocmSmiPath,
                Arguments = "--showproductname --showmeminfo=vram",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(processInfo);
            if (process == null) return devices;

            int deviceId = 0;
            using StreamReader reader = process.StandardOutput;
            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.Contains("GPU") && !line.Contains("VRAM"))
                {
                    // Parse GPU product name
                    string[] parts = line.Split(':', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2)
                    {
                        GpuDevice device = new()
                        {
                            DeviceId = deviceId,
                            DeviceName = parts[1],
                            DeviceType = "AMD",
                            MemoryMb = 0, // Will be updated from VRAM line
                            SupportedEncoders = new() { "h264_amf", "hevc_amf", "av1_amf" }
                        };
                        devices.Add(device);
                    }
                }
                else if (line.Contains("VRAM") && devices.Count > deviceId)
                {
                    string[] parts = line.Split(':', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2 && long.TryParse(Regex.Match(parts[1], @"\d+").Value, out long memoryMb))
                    {
                        devices[deviceId].MemoryMb = memoryMb;
                        Logger.App($"Detected AMD GPU: {devices[deviceId].DeviceName} ({memoryMb}MB)", Serilog.Events.LogEventLevel.Information);
                        deviceId++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to detect AMD GPUs via rocm-smi: {ex.Message}", Serilog.Events.LogEventLevel.Warning);
        }

        return devices;
    }

    /// <summary>
    /// Detect Intel Arc GPUs using intel_gpu_top
    /// </summary>
    private List<GpuDevice> DetectIntelDevices()
    {
        List<GpuDevice> devices = new();

        try
        {
            string intelGpuTopPath = FindExecutableInPath("intel_gpu_top");
            
            if (string.IsNullOrEmpty(intelGpuTopPath))
            {
                Logger.App("intel_gpu_top not found. Install intel-gpu-tools package for Intel GPU detection.", Serilog.Events.LogEventLevel.Debug);
                return devices;
            }

            ProcessStartInfo processInfo = new()
            {
                FileName = intelGpuTopPath,
                Arguments = "--device /dev/dri/card0 --iterations=1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(processInfo);
            if (process != null)
            {
                if (process.WaitForExit(5000))
                {
                    GpuDevice device = new()
                    {
                        DeviceId = 0,
                        DeviceName = "Intel Arc",
                        DeviceType = "Intel",
                        MemoryMb = 0,
                        SupportedEncoders = new() { "h264_qsv", "hevc_qsv", "av1_qsv" }
                    };

                    devices.Add(device);
                    Logger.App($"Detected Intel GPU: {device.DeviceName}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to detect Intel GPUs via intel_gpu_top: {ex.Message}", Serilog.Events.LogEventLevel.Warning);
        }

        return devices;
    }

    /// <summary>
    /// Find an executable in system PATH
    /// </summary>
    private string? FindExecutableInPath(string executableName)
    {
        try
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] paths = pathEnv.Split(Path.PathSeparator);

            foreach (string path in paths)
            {
                string fullPath = Path.Combine(path, executableName + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""));
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Error searching for {executableName} in PATH: {ex.Message}", Serilog.Events.LogEventLevel.Warning);
        }

        return null;
    }
}

/// <summary>
/// Represents a detected GPU device
/// </summary>
public class GpuDevice
{
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty; // "NVIDIA", "AMD", "Intel", etc.
    public long MemoryMb { get; set; }
    public string ComputeCapability { get; set; } = string.Empty; // For NVIDIA CUDA compute capability
    public List<string> SupportedEncoders { get; set; } = []; // h264_nvenc, hevc_nvenc, etc.
}
