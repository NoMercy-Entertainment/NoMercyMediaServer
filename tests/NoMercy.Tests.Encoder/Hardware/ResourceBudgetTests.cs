// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Hardware;

public class ResourceBudgetTests
{
    private static readonly GpuDevice TestGpu = new(
        Vendor: GpuVendor.Nvidia,
        Name: "RTX 4090",
        VramMb: 24576,
        MaxEncoderSessions: 3,
        SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
    );

    [Fact]
    public void InitialState_AllSlotsAvailable()
    {
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: TestGpu.Name).Should().Be(expected: 3);
        budget.AvailableCpuThreads().Should().Be(expected: 8);
    }

    [Fact]
    public void Acquire_GpuSlot_DecreasesAvailable()
    {
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
        ResourceLease lease = budget.Acquire(requirement: requirement);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: TestGpu.Name).Should().Be(expected: 2);
        lease.Should().NotBeNull();
    }

    [Fact]
    public void Release_RestoresSlots()
    {
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
        ResourceLease lease = budget.Acquire(requirement: requirement);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: TestGpu.Name).Should().Be(expected: 2);
        budget.Release(lease: lease);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: TestGpu.Name).Should().Be(expected: 3);
    }

    [Fact]
    public void Acquire_CpuThreads_DecreasesAvailable()
    {
        ResourceBudget budget = new(gpuDevices: [], cpuCores: 8);
        ResourceRequirement requirement = new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 4);
        ResourceLease lease = budget.Acquire(requirement: requirement);
        budget.AvailableCpuThreads().Should().Be(expected: 4);
        budget.Release(lease: lease);
        budget.AvailableCpuThreads().Should().Be(expected: 8);
    }

    [Fact]
    public void TryAcquire_WhenExhausted_ReturnsNull()
    {
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
        ResourceLease lease1 = budget.Acquire(requirement: requirement);
        ResourceLease lease2 = budget.Acquire(requirement: requirement);
        ResourceLease lease3 = budget.Acquire(requirement: requirement);
        ResourceLease? lease4 = budget.TryAcquire(requirement: requirement, timeout: TimeSpan.FromMilliseconds(milliseconds: 50));
        lease4.Should().BeNull();
        budget.Release(lease: lease1);
        budget.Release(lease: lease2);
        budget.Release(lease: lease3);
    }

    // ── IsGpuDeviceRegistered — Fillz's field bug: an AMF-pinned job on an
    // Nvidia-only host must be recognizable as "absent", not "busy". ────────

    [Fact]
    public void IsGpuDeviceRegistered_TrueForRegisteredDevice()
    {
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);

        budget.IsGpuDeviceRegistered(gpuDeviceKey: TestGpu.Name).Should().BeTrue();
    }

    [Fact]
    public void IsGpuDeviceRegistered_TrueForVendorAliasOfRegisteredDevice()
    {
        // Nvidia() only device — "nvenc" and "h264_nvenc" are vendor/encoder
        // aliases of the same semaphore, not separate devices.
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);

        budget.IsGpuDeviceRegistered(gpuDeviceKey: "nvenc").Should().BeTrue();
        budget.IsGpuDeviceRegistered(gpuDeviceKey: "h264_nvenc").Should().BeTrue();
    }

    [Fact]
    public void IsGpuDeviceRegistered_FalseWhenVendorNeverPresent()
    {
        // Only an NVIDIA GPU is registered — an AMD-only key (Fillz's stuck
        // "h264_amf" child jobs) must read as permanently absent, never busy.
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);

        budget.IsGpuDeviceRegistered(gpuDeviceKey: "h264_amf").Should().BeFalse();
        budget.IsGpuDeviceRegistered(gpuDeviceKey: "amf").Should().BeFalse();
    }

    [Fact]
    public void IsGpuDeviceRegistered_FalseWhenNoGpuAtAll()
    {
        ResourceBudget budget = new(gpuDevices: [], cpuCores: 8);

        budget.IsGpuDeviceRegistered(gpuDeviceKey: "h264_nvenc").Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_WhenCpuHeadroomExceeded_ReturnsNull()
    {
        // Monitor reports system CPU at 90 %; headroom threshold is 75 %. The
        // semaphore has slots free but the gate must still deny.
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(expression: m => m.GetSystemCpuUsagePercent()).Returns(value: 90.0);
        monitor.Setup(expression: m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(value: 0);
        monitor.Setup(expression: m => m.GetAvailableMemoryMb()).Returns(value: 8192);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8, monitor: monitor.Object, options: options);

        ResourceLease? lease = budget.TryAcquire(
            requirement: new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 1),
            timeout: TimeSpan.Zero
        );

        lease.Should().BeNull();
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: TestGpu.Name).Should().Be(expected: 3);
        budget.AvailableCpuThreads().Should().Be(expected: 8);
    }

    [Fact]
    public void TryAcquire_WhenHeadroomBelowThreshold_GrantsLease()
    {
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(expression: m => m.GetSystemCpuUsagePercent()).Returns(value: 40.0);
        monitor.Setup(expression: m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(value: 0);
        monitor.Setup(expression: m => m.GetAvailableMemoryMb()).Returns(value: 8192);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8, monitor: monitor.Object, options: options);

        ResourceLease? lease = budget.TryAcquire(
            requirement: new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 1),
            timeout: TimeSpan.Zero
        );

        lease.Should().NotBeNull();
        budget.Release(lease: lease!);
    }

    [Fact]
    public void TryAcquire_WhenGpuEncodeUtilSaturated_ReturnsNull()
    {
        // Monitor returns GPU encode util as fraction (0.0–1.0). 0.95 -> 95 %
        // which exceeds the 80 % threshold.
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(expression: m => m.GetSystemCpuUsagePercent()).Returns(value: 20.0);
        monitor.Setup(expression: m => m.GetGpuEncodeUtilization(TestGpu.Name)).Returns(value: 0.95);
        monitor.Setup(expression: m => m.GetAvailableMemoryMb()).Returns(value: 8192);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8, monitor: monitor.Object, options: options);

        ResourceLease? lease = budget.TryAcquire(
            requirement: new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 0),
            timeout: TimeSpan.Zero
        );

        lease.Should().BeNull();
    }

    [Fact]
    public void TryAcquire_WhenMemoryBelowMinimum_ReturnsNull()
    {
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(expression: m => m.GetSystemCpuUsagePercent()).Returns(value: 20.0);
        monitor.Setup(expression: m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(value: 0);
        monitor.Setup(expression: m => m.GetAvailableMemoryMb()).Returns(value: 256);

        ResourceBudgetOptions options = new(
            CpuHeadroomPercent: 75,
            GpuHeadroomPercent: 80,
            MinFreeMemoryMb: 1024
        );
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8, monitor: monitor.Object, options: options);

        ResourceLease? lease = budget.TryAcquire(
            requirement: new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 1),
            timeout: TimeSpan.Zero
        );

        lease.Should().BeNull();
    }

    [Fact]
    public void TryAcquire_WithDisabledOptions_IgnoresMonitor()
    {
        // Saturated host but every threshold is 0 → headroom gate is disabled
        // and only the static semaphores govern.
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(expression: m => m.GetSystemCpuUsagePercent()).Returns(value: 99.0);
        monitor.Setup(expression: m => m.GetGpuEncodeUtilization(It.IsAny<string>())).Returns(value: 0.99);
        monitor.Setup(expression: m => m.GetAvailableMemoryMb()).Returns(value: 64);

        ResourceBudget budget = new(
            gpuDevices: [TestGpu],
            cpuCores: 8,
            monitor: monitor.Object,
            options: ResourceBudgetOptions.Disabled
        );

        ResourceLease? lease = budget.TryAcquire(
            requirement: new(GpuDeviceKey: TestGpu.Name, GpuSlots: 1, CpuThreads: 1),
            timeout: TimeSpan.Zero
        );

        lease.Should().NotBeNull();
        budget.Release(lease: lease!);
    }

    [Fact]
    public async Task ConcurrentAcquire_IsThreadSafe()
    {
        ResourceBudget budget = new(gpuDevices: [TestGpu], cpuCores: 8);
        ResourceRequirement requirement = new(
            GpuDeviceKey: TestGpu.Name,
            GpuSlots: 1,
            CpuThreads: 0
        );
        int successCount = 0;
        List<ResourceLease> leases = [];
        object lockObj = new();

        Task[] tasks = Enumerable
            .Range(start: 0, count: 10)
            .Select(selector: _ =>
                Task.Run(action: () =>
                {
                    ResourceLease? lease = budget.TryAcquire(
                        requirement: requirement,
                        timeout: TimeSpan.FromMilliseconds(milliseconds: 100)
                    );
                    if (lease is not null)
                    {
                        Interlocked.Increment(location: ref successCount);
                        lock (lockObj)
                        {
                            leases.Add(item: lease);
                        }
                    }
                })
            )
            .ToArray();

        await Task.WhenAll(tasks: tasks);
        successCount.Should().Be(expected: 3);
        foreach (ResourceLease lease in leases)
            budget.Release(lease: lease);
    }
}
