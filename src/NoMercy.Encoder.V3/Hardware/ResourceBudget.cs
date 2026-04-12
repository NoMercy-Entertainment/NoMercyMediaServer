namespace NoMercy.Encoder.V3.Hardware;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public class ResourceBudget : IResourceBudget
{
    private readonly ConcurrentDictionary<GpuDevice, SemaphoreSlim> _gpuSemaphores;
    private readonly SemaphoreSlim _cpuSemaphore;
    private readonly ILogger<ResourceBudget>? _logger;

    public ResourceBudget(
        IReadOnlyList<GpuDevice> gpuDevices,
        int cpuCores,
        ILogger<ResourceBudget>? logger = null
    )
    {
        _logger = logger;
        _cpuSemaphore = new SemaphoreSlim(cpuCores, cpuCores);

        _gpuSemaphores = new ConcurrentDictionary<GpuDevice, SemaphoreSlim>();

        foreach (GpuDevice device in gpuDevices)
        {
            _gpuSemaphores[device] = new SemaphoreSlim(
                device.MaxEncoderSessions,
                device.MaxEncoderSessions
            );
        }
    }

    public int AvailableGpuEncoderSlots(GpuDevice device)
    {
        return _gpuSemaphores.TryGetValue(device, out SemaphoreSlim? semaphore)
            ? semaphore.CurrentCount
            : 0;
    }

    public int AvailableCpuThreads()
    {
        return _cpuSemaphore.CurrentCount;
    }

    public ResourceLease Acquire(ResourceRequirement requirement)
    {
        if (requirement.GpuDevice is not null && requirement.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDevice);

            for (int i = 0; i < requirement.GpuSlots; i++)
            {
                gpuSemaphore.Wait();
            }

            _logger?.LogDebug(
                "Acquired {GpuSlots} GPU slot(s) on {GpuName}",
                requirement.GpuSlots,
                requirement.GpuDevice.Name
            );
        }

        if (requirement.CpuThreads > 0)
        {
            for (int i = 0; i < requirement.CpuThreads; i++)
            {
                _cpuSemaphore.Wait();
            }

            _logger?.LogDebug("Acquired {CpuThreads} CPU thread(s)", requirement.CpuThreads);
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug("Lease {LeaseId} granted", leaseId);

        return new ResourceLease(
            leaseId,
            requirement.GpuDevice,
            requirement.GpuSlots,
            requirement.CpuThreads
        );
    }

    public ResourceLease? TryAcquire(ResourceRequirement requirement, TimeSpan timeout)
    {
        int timeoutMs = (int)timeout.TotalMilliseconds;

        if (requirement.GpuDevice is not null && requirement.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDevice);
            int acquiredGpuSlots = 0;

            for (int i = 0; i < requirement.GpuSlots; i++)
            {
                if (!gpuSemaphore.Wait(timeoutMs))
                {
                    // Rollback partially acquired GPU slots
                    for (int j = 0; j < acquiredGpuSlots; j++)
                    {
                        gpuSemaphore.Release();
                    }

                    _logger?.LogDebug(
                        "TryAcquire timed out acquiring GPU slot {Slot}/{Total} on {GpuName}",
                        i + 1,
                        requirement.GpuSlots,
                        requirement.GpuDevice.Name
                    );

                    return null;
                }

                acquiredGpuSlots++;
            }
        }

        if (requirement.CpuThreads > 0)
        {
            int acquiredCpuThreads = 0;

            for (int i = 0; i < requirement.CpuThreads; i++)
            {
                if (!_cpuSemaphore.Wait(timeoutMs))
                {
                    // Rollback partially acquired CPU threads
                    for (int j = 0; j < acquiredCpuThreads; j++)
                    {
                        _cpuSemaphore.Release();
                    }

                    // Rollback GPU slots already acquired in this TryAcquire call
                    if (requirement.GpuDevice is not null && requirement.GpuSlots > 0)
                    {
                        SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDevice);

                        for (int k = 0; k < requirement.GpuSlots; k++)
                        {
                            gpuSemaphore.Release();
                        }
                    }

                    _logger?.LogDebug(
                        "TryAcquire timed out acquiring CPU thread {Thread}/{Total}, rolled back GPU slots",
                        i + 1,
                        requirement.CpuThreads
                    );

                    return null;
                }

                acquiredCpuThreads++;
            }
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug("Lease {LeaseId} granted via TryAcquire", leaseId);

        return new ResourceLease(
            leaseId,
            requirement.GpuDevice,
            requirement.GpuSlots,
            requirement.CpuThreads
        );
    }

    public void Release(ResourceLease lease)
    {
        if (lease.GpuDevice is not null && lease.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(lease.GpuDevice);
            gpuSemaphore.Release(lease.GpuSlots);

            _logger?.LogDebug(
                "Released {GpuSlots} GPU slot(s) on {GpuName} for lease {LeaseId}",
                lease.GpuSlots,
                lease.GpuDevice.Name,
                lease.LeaseId
            );
        }

        if (lease.CpuThreads > 0)
        {
            _cpuSemaphore.Release(lease.CpuThreads);

            _logger?.LogDebug(
                "Released {CpuThreads} CPU thread(s) for lease {LeaseId}",
                lease.CpuThreads,
                lease.LeaseId
            );
        }
    }

    private SemaphoreSlim GetGpuSemaphore(GpuDevice device)
    {
        if (!_gpuSemaphores.TryGetValue(device, out SemaphoreSlim? semaphore))
        {
            throw new InvalidOperationException(
                $"GPU device '{device.Name}' is not registered with this ResourceBudget."
            );
        }

        return semaphore;
    }
}
