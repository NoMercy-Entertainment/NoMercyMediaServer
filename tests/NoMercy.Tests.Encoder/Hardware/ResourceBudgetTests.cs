using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Hardware;

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
        budget.AvailableGpuEncoderSlots(TestGpu.Name).Should().Be(3);
        budget.AvailableCpuThreads().Should().Be(8);
    }

    [Fact]
    public void Acquire_GpuSlot_DecreasesAvailable()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
        ResourceLease lease = budget.Acquire(requirement);
        budget.AvailableGpuEncoderSlots(TestGpu.Name).Should().Be(2);
        lease.Should().NotBeNull();
    }

    [Fact]
    public void Release_RestoresSlots()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
        ResourceLease lease = budget.Acquire(requirement);
        budget.AvailableGpuEncoderSlots(TestGpu.Name).Should().Be(2);
        budget.Release(lease);
        budget.AvailableGpuEncoderSlots(TestGpu.Name).Should().Be(3);
    }

    [Fact]
    public void Acquire_CpuThreads_DecreasesAvailable()
    {
        ResourceBudget budget = new([], cpuCores: 8);
        ResourceRequirement requirement = new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 4);
        ResourceLease lease = budget.Acquire(requirement);
        budget.AvailableCpuThreads().Should().Be(4);
        budget.Release(lease);
        budget.AvailableCpuThreads().Should().Be(8);
    }

    [Fact]
    public void TryAcquire_WhenExhausted_ReturnsNull()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
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
    public void TryAcquire_WhenCpuHeadroomExceeded_ReturnsNull()
    {
        // Monitor reports system CPU at 90 %; headroom threshold is 75 %. The
        // semaphore has slots free but the gate must still deny.
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(m => m.GetSystemCpuUsagePercent()).Returns(90.0);
        monitor.Setup(m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(0);
        monitor.Setup(m => m.GetAvailableMemoryMb()).Returns(8192);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new([TestGpu], cpuCores: 8, monitor.Object, options);

        ResourceLease? lease = budget.TryAcquire(
            new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 1),
            TimeSpan.Zero
        );

        lease.Should().BeNull();
        budget.AvailableGpuEncoderSlots(TestGpu.Name).Should().Be(3);
        budget.AvailableCpuThreads().Should().Be(8);
    }

    [Fact]
    public void TryAcquire_WhenHeadroomBelowThreshold_GrantsLease()
    {
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(m => m.GetSystemCpuUsagePercent()).Returns(40.0);
        monitor.Setup(m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(0);
        monitor.Setup(m => m.GetAvailableMemoryMb()).Returns(8192);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new([TestGpu], cpuCores: 8, monitor.Object, options);

        ResourceLease? lease = budget.TryAcquire(
            new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 1),
            TimeSpan.Zero
        );

        lease.Should().NotBeNull();
        budget.Release(lease!);
    }

    [Fact]
    public void TryAcquire_WhenGpuEncodeUtilSaturated_ReturnsNull()
    {
        // Monitor returns GPU encode util as fraction (0.0–1.0). 0.95 -> 95 %
        // which exceeds the 80 % threshold.
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(m => m.GetSystemCpuUsagePercent()).Returns(20.0);
        monitor.Setup(m => m.GetGpuEncodeUtilization(TestGpu.Name)).Returns(0.95);
        monitor.Setup(m => m.GetAvailableMemoryMb()).Returns(8192);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new([TestGpu], cpuCores: 8, monitor.Object, options);

        ResourceLease? lease = budget.TryAcquire(
            new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 0),
            TimeSpan.Zero
        );

        lease.Should().BeNull();
    }

    [Fact]
    public void TryAcquire_WhenMemoryBelowMinimum_ReturnsNull()
    {
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(m => m.GetSystemCpuUsagePercent()).Returns(20.0);
        monitor.Setup(m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(0);
        monitor.Setup(m => m.GetAvailableMemoryMb()).Returns(256);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new([TestGpu], cpuCores: 8, monitor.Object, options);

        ResourceLease? lease = budget.TryAcquire(
            new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 1),
            TimeSpan.Zero
        );

        lease.Should().BeNull();
    }

    [Fact]
    public void TryAcquire_WithDisabledOptions_IgnoresMonitor()
    {
        // Saturated host but every threshold is 0 → headroom gate is disabled
        // and only the static semaphores govern.
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(m => m.GetSystemCpuUsagePercent()).Returns(99.0);
        monitor.Setup(m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(0.99);
        monitor.Setup(m => m.GetAvailableMemoryMb()).Returns(64);

        ResourceBudget budget = new(
            [TestGpu],
            cpuCores: 8,
            monitor.Object,
            ResourceBudgetOptions.Disabled
        );

        ResourceLease? lease = budget.TryAcquire(
            new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 1),
            TimeSpan.Zero
        );

        lease.Should().NotBeNull();
        budget.Release(lease!);
    }

    [Fact]
    public async Task ConcurrentAcquire_IsThreadSafe()
    {
        ResourceBudget budget = new([TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
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
