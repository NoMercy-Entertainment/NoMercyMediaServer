using System.Runtime.InteropServices;
using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem;

public class Info
{
    public static string DeviceName { get; set; } = Environment.MachineName;
    public static readonly Guid DeviceId = Software.GetDeviceId();
    public static readonly string Os = RuntimeInformation.OSDescription;
    public static readonly string Platform = Software.GetPlatform();
    public static readonly string Architecture = RuntimeInformation.ProcessArchitecture.ToString();
    public static readonly List<string> CpuVendors = Cpu.Vendors();
    public static readonly List<string> CpuNames = Cpu.Names();
    public static readonly List<string> GpuVendors = Gpu.Vendors();
    public static readonly List<string> GpuNames = Gpu.Names();
    public static readonly string? OsVersion = Software.GetSystemVersion();
    public static readonly DateTime BootTime = Software.GetBootTime();
    public static readonly DateTime StartTime = DateTime.UtcNow;
    public static readonly string ExecSuffix = Platform == "windows" ? ".exe" : "";


}