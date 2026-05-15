using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NoMercy.Resources;

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Production implementation of <see cref="IResourceBudget"/>.
/// GPU devices are keyed by their canonical name string so this class can be
/// constructed from either <see cref="GpuDevice"/> lists or plain strings.
/// </summary>
public class ResourceBudget : IResourceBudget
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gpuSemaphores;
    private readonly SemaphoreSlim _cpuSemaphore;
    private readonly IResourceMonitor? _monitor;
    private readonly ILogger<ResourceBudget>? _logger;

    public ResourceBudget(
        IReadOnlyList<GpuDevice> gpuDevices,
        int cpuCores,
        IResourceMonitor? monitor = null,
        ILogger<ResourceBudget>? logger = null
    )
    {
        _monitor = monitor;
        _logger = logger;
        _cpuSemaphore = new(cpuCores, cpuCores);
        _gpuSemaphores = new();

        foreach (GpuDevice device in gpuDevices)
        {
            _gpuSemaphores[device.Name] = new(device.MaxEncoderSessions, device.MaxEncoderSessions);
        }
    }

    public int AvailableGpuEncoderSlots(string gpuDeviceKey)
    {
        return _gpuSemaphores.TryGetValue(gpuDeviceKey, out SemaphoreSlim? semaphore)
            ? semaphore.CurrentCount
            : 0;
    }

    public double CurrentGpuEncodeUtilization(string gpuDeviceKey) =>
        _monitor?.GetGpuEncodeUtilization(gpuDeviceKey) ?? 0.0;

    public int AvailableCpuThreads()
    {
        return _cpuSemaphore.CurrentCount;
    }

    public ResourceLease Acquire(ResourceRequirement requirement)
    {
        if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDeviceKey);

            for (int slotIndex = 0; slotIndex < requirement.GpuSlots; slotIndex++)
            {
                gpuSemaphore.Wait();
            }

            _logger?.LogDebug(
                "Acquired {GpuSlots} GPU slot(s) on {GpuKey}",
                requirement.GpuSlots,
                requirement.GpuDeviceKey
            );
        }

        if (requirement.CpuThreads > 0)
        {
            for (int threadIndex = 0; threadIndex < requirement.CpuThreads; threadIndex++)
            {
                _cpuSemaphore.Wait();
            }

            _logger?.LogDebug("Acquired {CpuThreads} CPU thread(s)", requirement.CpuThreads);
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug("Lease {LeaseId} granted", leaseId);

        return new(leaseId, requirement.GpuDeviceKey, requirement.GpuSlots, requirement.CpuThreads);
    }

    public ResourceLease? TryAcquire(ResourceRequirement requirement, TimeSpan timeout)
    {
        int timeoutMs = (int)timeout.TotalMilliseconds;

        if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDeviceKey);
            int acquiredGpuSlots = 0;

            for (int slotIndex = 0; slotIndex < requirement.GpuSlots; slotIndex++)
            {
                if (!gpuSemaphore.Wait(timeoutMs))
                {
                    for (int rollback = 0; rollback < acquiredGpuSlots; rollback++)
                    {
                        gpuSemaphore.Release();
                    }

                    _logger?.LogDebug(
                        "TryAcquire timed out acquiring GPU slot {Slot}/{Total} on {GpuKey}",
                        slotIndex + 1,
                        requirement.GpuSlots,
                        requirement.GpuDeviceKey
                    );

                    return null;
                }

                acquiredGpuSlots++;
            }
        }

        if (requirement.CpuThreads > 0)
        {
            int acquiredCpuThreads = 0;

            for (int threadIndex = 0; threadIndex < requirement.CpuThreads; threadIndex++)
            {
                if (!_cpuSemaphore.Wait(timeoutMs))
                {
                    for (int rollback = 0; rollback < acquiredCpuThreads; rollback++)
                    {
                        _cpuSemaphore.Release();
                    }

                    if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
                    {
                        SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDeviceKey);

                        for (int rollback = 0; rollback < requirement.GpuSlots; rollback++)
                        {
                            gpuSemaphore.Release();
                        }
                    }

                    _logger?.LogDebug(
                        "TryAcquire timed out acquiring CPU thread {Thread}/{Total}, rolled back GPU slots",
                        threadIndex + 1,
                        requirement.CpuThreads
                    );

                    return null;
                }

                acquiredCpuThreads++;
            }
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug("Lease {LeaseId} granted via TryAcquire", leaseId);

        return new(leaseId, requirement.GpuDeviceKey, requirement.GpuSlots, requirement.CpuThreads);
    }

    public void Release(ResourceLease lease)
    {
        if (lease.GpuDeviceKey is not null && lease.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(lease.GpuDeviceKey);
            gpuSemaphore.Release(lease.GpuSlots);

            _logger?.LogDebug(
                "Released {GpuSlots} GPU slot(s) on {GpuKey} for lease {LeaseId}",
                lease.GpuSlots,
                lease.GpuDeviceKey,
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

    private SemaphoreSlim GetGpuSemaphore(string gpuDeviceKey)
    {
        if (!_gpuSemaphores.TryGetValue(gpuDeviceKey, out SemaphoreSlim? semaphore))
        {
            throw new InvalidOperationException(
                $"GPU device '{gpuDeviceKey}' is not registered with this ResourceBudget."
            );
        }

        return semaphore;
    }
}
