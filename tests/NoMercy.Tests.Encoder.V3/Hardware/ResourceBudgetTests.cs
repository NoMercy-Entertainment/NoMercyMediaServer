namespace NoMercy.Tests.Encoder.V3.Hardware;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;

public class ResourceBudgetTests
{
    private static readonly GpuDevice TestGpu = new(
        GpuVendor.Nvidia,
        "RTX 4090",
        24576,
        3,
        [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
    );

    [Fact]
    public void InitialState_AllSlotsAvailable()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        budget.AvailableGpuEncoderSlots(TestGpu).Should().Be(3);
        budget.AvailableCpuThreads().Should().Be(8);
    }

    [Fact]
    public void Acquire_GpuSlot_DecreasesAvailable()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(GpuDevice: TestGpu, GpuSlots: 1, CpuThreads: 0);
        ResourceLease lease = budget.Acquire(requirement);
        budget.AvailableGpuEncoderSlots(TestGpu).Should().Be(2);
        lease.Should().NotBeNull();
    }

    [Fact]
    public void Release_RestoresSlots()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(GpuDevice: TestGpu, GpuSlots: 1, CpuThreads: 0);
        ResourceLease lease = budget.Acquire(requirement);
        budget.AvailableGpuEncoderSlots(TestGpu).Should().Be(2);
        budget.Release(lease);
        budget.AvailableGpuEncoderSlots(TestGpu).Should().Be(3);
    }

    [Fact]
    public void Acquire_CpuThreads_DecreasesAvailable()
    {
        ResourceBudget budget = new([], cpuCores: 8);
        ResourceRequirement requirement = new(GpuDevice: null, GpuSlots: 0, CpuThreads: 4);
        ResourceLease lease = budget.Acquire(requirement);
        budget.AvailableCpuThreads().Should().Be(4);
        budget.Release(lease);
        budget.AvailableCpuThreads().Should().Be(8);
    }

    [Fact]
    public void TryAcquire_WhenExhausted_ReturnsNull()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(GpuDevice: TestGpu, GpuSlots: 1, CpuThreads: 0);
        ResourceLease lease1 = budget.Acquire(requirement);
        ResourceLease lease2 = budget.Acquire(requirement);
        ResourceLease lease3 = budget.Acquire(requirement);
        ResourceLease? lease4 = budget.TryAcquire(requirement, TimeSpan.FromMilliseconds(50));
        lease4.Should().BeNull();
        budget.Release(lease1);
        budget.Release(lease2);
        budget.Release(lease3);
    }

    [Fact]
    public async Task ConcurrentAcquire_IsThreadSafe()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(GpuDevice: TestGpu, GpuSlots: 1, CpuThreads: 0);
        int successCount = 0;
        List<ResourceLease> leases = [];
        object lockObj = new();

        Task[] tasks = Enumerable
            .Range(0, 10)
            .Select(_ =>
                Task.Run(() =>
                {
                    ResourceLease? lease = budget.TryAcquire(
                        requirement,
                        TimeSpan.FromMilliseconds(100)
                    );
                    if (lease is not null)
                    {
                        Interlocked.Increment(ref successCount);
                        lock (lockObj)
                        {
                            leases.Add(lease);
                        }
                    }
                })
            )
            .ToArray();

        await Task.WhenAll(tasks);
        successCount.Should().Be(3);
        foreach (ResourceLease lease in leases)
            budget.Release(lease);
    }
}
