namespace NoMercy.Resources;

/// <summary>
/// Per-process GPU sample emitted by <see cref="IResourceMonitor.SampleGpu"/>.
/// When vendor-specific telemetry is unavailable the list is empty — callers
/// must treat a missing entry as "unknown", not as "idle".
/// </summary>
public sealed record GpuProcessSample(
    int Pid,
    int GpuIndex,
    int EncoderUtilizationPercent,
    long EncoderMemoryBytes
);

/// <summary>
/// Provides live resource telemetry to <see cref="IResourceBudget"/> and to
/// the hardware benchmark. GPU devices are identified by key string (matching
/// <c>GpuDevice.Name</c>) so this interface carries no encoder dependency.
/// </summary>
public interface IResourceMonitor
{
    double GetCpuUsagePercent();
    double GetGpuEncodeUtilization(string gpuDeviceKey);
    long GetAvailableMemoryMb();

    /// <summary>
    /// Returns per-process GPU encoder utilization for every ffmpeg process the
    /// vendor runtime can observe. Returns an empty list on platforms or
    /// configurations where GPU telemetry is unavailable.
    /// </summary>
    IReadOnlyList<GpuProcessSample> SampleGpu();
}
