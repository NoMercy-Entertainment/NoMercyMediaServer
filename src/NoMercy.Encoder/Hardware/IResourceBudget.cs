namespace NoMercy.Encoder.Hardware;

public interface IResourceBudget
{
    int AvailableGpuEncoderSlots(GpuDevice device);

    double CurrentGpuEncodeUtilization(GpuDevice device);

    int AvailableCpuThreads();

    ResourceLease Acquire(ResourceRequirement requirement);

    ResourceLease? TryAcquire(ResourceRequirement requirement, TimeSpan timeout);

    void Release(ResourceLease lease);
}
