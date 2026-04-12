namespace NoMercy.Encoder.V3.Hardware;

public interface IResourceBudget
{
    int AvailableGpuEncoderSlots(GpuDevice device);

    int AvailableCpuThreads();

    ResourceLease Acquire(ResourceRequirement requirement);

    ResourceLease? TryAcquire(ResourceRequirement requirement, TimeSpan timeout);

    void Release(ResourceLease lease);
}
