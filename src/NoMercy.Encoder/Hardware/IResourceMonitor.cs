namespace NoMercy.Encoder.Hardware;

public interface IResourceMonitor
{
    double GetCpuUsagePercent();
    double GetGpuEncodeUtilization(GpuDevice device);
    long GetAvailableMemoryMb();
}
