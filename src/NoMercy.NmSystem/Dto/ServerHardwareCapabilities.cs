using NoMercy.NmSystem.Information;
using Newtonsoft.Json;

namespace NoMercy.NmSystem.Dto;

/// <summary>
/// Server hardware capabilities
/// Provides complete system capability information including GPU acceleration support
/// </summary>
public class ServerHardwareCapabilities
{
    [JsonProperty("cpu_cores")]
    public int CpuCores { get; set; }

    [JsonProperty("gpu_count")]
    public int GpuCount { get; set; }

    [JsonProperty("total_memory_gb")]
    public long TotalMemoryGb { get; set; }

    [JsonProperty("supports_hw_accel")]
    public bool SupportsHwAccel { get; set; }

    [JsonProperty("gpu_model")]
    public string? GpuModel { get; set; }

    /// <summary>
    /// Get current system hardware capabilities
    /// </summary>
    public static ServerHardwareCapabilities Current => Hardware.GetCapabilities();
}
