namespace NoMercy.Encoder.Hardware;

using System.Diagnostics;

/// <summary>
/// Cross-platform <see cref="IResourceMonitor"/> that reads CPU and memory
/// metrics from the host process + <see cref="System.Diagnostics"/>. GPU
/// utilization stays at 0 — reading it reliably needs a vendor-specific
/// tool (nvidia-smi, rocm-smi, intel_gpu_top) that ships with drivers but
/// not the .NET runtime. Plugins can replace this with a vendor-aware
/// implementation when they need real GPU numbers.
/// </summary>
public class ProcessResourceMonitor : IResourceMonitor
{
    // Cache last CPU snapshot so percentage reports relative work between
    // calls instead of the total-since-process-start value.
    private DateTime _lastSnapshotAt = DateTime.UtcNow;
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private readonly Lock _snapshotLock = new();

    public double GetCpuUsagePercent()
    {
        using Process current = Process.GetCurrentProcess();
        TimeSpan cpuTime = current.TotalProcessorTime;
        DateTime now = DateTime.UtcNow;

        lock (_snapshotLock)
        {
            double elapsedMs = (now - _lastSnapshotAt).TotalMilliseconds;
            double cpuMs = (cpuTime - _lastCpuTime).TotalMilliseconds;

            _lastSnapshotAt = now;
            _lastCpuTime = cpuTime;

            if (elapsedMs < 1)
                return 0;

            int cores = Math.Max(1, Environment.ProcessorCount);
            double rawPercent = (cpuMs / elapsedMs) / cores * 100.0;
            return Math.Clamp(rawPercent, 0, 100);
        }
    }

    public double GetGpuEncodeUtilization(GpuDevice device) => 0.0;

    public long GetAvailableMemoryMb()
    {
        // GC.GetGCMemoryInfo gives the host's perspective on available
        // memory at the GC level. It's not a perfect proxy for system
        // memory but it's cross-platform and framework-maintained, which
        // beats spawning `free` or `wmic` on every query.
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        long available = info.TotalAvailableMemoryBytes - info.MemoryLoadBytes;
        return Math.Max(0, available / (1024 * 1024));
    }
}

/// <summary>
/// Zero-valued monitor for installs that don't care about live resource
/// metrics (e.g. tests or CLI-only runs). Registered when no real monitor
/// is available so ResourceBudget + friends always have a non-null
/// dependency.
/// </summary>
public sealed class NullResourceMonitor : IResourceMonitor
{
    public double GetCpuUsagePercent() => 0;

    public double GetGpuEncodeUtilization(GpuDevice device) => 0;

    public long GetAvailableMemoryMb() => 0;
}
