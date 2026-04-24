namespace NoMercy.Encoder.Hardware;

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

public interface IResourceMonitor
{
    double GetCpuUsagePercent();
    double GetGpuEncodeUtilization(GpuDevice device);
    long GetAvailableMemoryMb();

    /// <summary>
    /// Returns per-process GPU encoder utilization for every ffmpeg process the
    /// vendor runtime can observe. Returns an empty list on platforms or
    /// configurations where GPU telemetry is unavailable — see
    /// <see cref="NvmlGpuSampler"/> for the NVIDIA implementation scaffold.
    /// </summary>
    IReadOnlyList<GpuProcessSample> SampleGpu();
}
