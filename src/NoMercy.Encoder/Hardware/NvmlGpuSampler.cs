namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Scaffold for an NVIDIA NVML-backed <see cref="IResourceMonitor"/> that
/// returns real per-process GPU encoder utilization. This class is intentionally
/// left as a no-op stub — the P/Invoke bindings or nvidia-smi shell-out
/// implementation land in a follow-up commit once the interface is stable.
///
/// To enable: register <see cref="NvmlGpuSampler"/> instead of
/// <see cref="ProcessResourceMonitor"/> in the DI container on NVIDIA hosts.
/// </summary>
public sealed class NvmlGpuSampler : ProcessResourceMonitor
{
    // Inherits the cross-platform CPU/memory implementation from
    // ProcessResourceMonitor. Only SampleGpu() needs to be overridden
    // once the NVML P/Invoke layer is wired up.
    //
    // Planned override:
    //   public override IReadOnlyList<GpuProcessSample> SampleGpu()
    //   {
    //       // Call nvmlDeviceGetProcessUtilization() for each device,
    //       // map back to GpuProcessSample records, return.
    //   }
}
