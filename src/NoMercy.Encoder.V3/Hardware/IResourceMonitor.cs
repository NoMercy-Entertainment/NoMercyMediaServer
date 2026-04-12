namespace NoMercy.Encoder.V3.Hardware;

public interface IResourceMonitor
{
    double GetCpuUsagePercent();
    double GetGpuEncodeUtilization(GpuDevice device);
    long GetAvailableMemoryMb();
}
