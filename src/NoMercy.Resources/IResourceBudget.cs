namespace NoMercy.Resources;

/// <summary>
/// Tracks available encoder capacity and gates dispatch so the system
/// never exceeds hardware session limits.
///
/// GPU devices are identified by their canonical name string (from
/// <c>GpuDevice.Name</c>) so this interface has no dependency on the
/// encoder-specific <c>GpuDevice</c> record.
/// </summary>
public interface IResourceBudget
{
    int AvailableGpuEncoderSlots(string gpuDeviceKey);

    double CurrentGpuEncodeUtilization(string gpuDeviceKey);

    int AvailableCpuThreads();

    ResourceLease Acquire(ResourceRequirement requirement);

    /// <summary>
    /// Async variant of <see cref="Acquire"/>. Live transcode workers run on
    /// the thread pool — calling the sync version blocks a worker thread for
    /// the entire wait, which starves the pool under load. Always prefer this
    /// from async callers.
    /// </summary>
    Task<ResourceLease> AcquireAsync(
        ResourceRequirement requirement,
        CancellationToken cancellationToken = default
    );

    ResourceLease? TryAcquire(ResourceRequirement requirement, TimeSpan timeout);

    void Release(ResourceLease lease);
}
