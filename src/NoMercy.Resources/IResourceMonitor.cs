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

    /// <summary>
    /// System-wide CPU utilization across every process on the host (not just
    /// the server). This is the signal the dispatch gate consults before
    /// granting a new encoder lease — <see cref="GetCpuUsagePercent"/> only
    /// counts the server process itself, so ffmpeg child processes are
    /// invisible to it and the budget never sees the encoder load it caused.
    /// </summary>
    double GetSystemCpuUsagePercent();

    double GetGpuEncodeUtilization(string gpuDeviceKey);
    long GetAvailableMemoryMb();

    /// <summary>
    /// Returns per-process GPU encoder utilization for every ffmpeg process the
    /// vendor runtime can observe. Returns an empty list on platforms or
    /// configurations where GPU telemetry is unavailable.
    /// </summary>
    IReadOnlyList<GpuProcessSample> SampleGpu();
}
