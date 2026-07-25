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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class ResourceAllocatorTests
{
    private static readonly IResourceMonitor NullMonitor = new NullResourceMonitor();

    private static IHardwareCapabilities MakeGpuCaps() =>
        new HardwareCapabilities(
            [
                new(
                    GpuVendor.Nvidia,
                    "RTX 4090",
                    24576,
                    12,
                    [VideoCodecType.H264, VideoCodecType.H265]
                ),
            ],
            16
        );

    private static IHardwareCapabilities CpuOnly => new HardwareCapabilities([], 8);

    private static List<ExecutionGroup> MakeGroups(int encodeCount, bool requiresGpu)
    {
        List<ExecutionNode> allNodes = [new("node_0", OperationType.Decode, [], new())];
        for (int i = 0; i < encodeCount; i++)
        {
            allNodes.Add(new($"encode_{i}", OperationType.Encode, ["node_0"], new()));
        }

        return
        [
            new(
                "group_0",
                allNodes.ToArray(),
                requiresGpu ? "RTX 4090" : null,
                requiresGpu ? encodeCount : 0,
                requiresGpu ? 0 : 4,
                requiresGpu,
                1
            ),
        ];
    }

    // ------------------------------------------------------------------
    // Memory ceiling tests
    // ------------------------------------------------------------------

    [Fact]
    public void CheckMemoryCeiling_WhenUnder75Percent_ReturnsTrue()
    {
        ResourceAllocator allocator = new(MakeGpuCaps(), NullMonitor);

        List<ExecutionGroup> groups = MakeGroups(1, true);

        bool result = allocator.CheckMemoryCeiling(groups, 8192);

        result.Should().BeTrue();
    }

    [Fact]
    public void CheckMemoryCeiling_WhenOver75Percent_ReturnsFalse()
    {
        ResourceAllocator allocator = new(MakeGpuCaps(), NullMonitor);

        List<ExecutionGroup> groups = MakeGroups(100, true);

        bool result = allocator.CheckMemoryCeiling(groups, 1024);

        result.Should().BeFalse();
    }

    [Fact]
    public void CheckMemoryCeiling_ExactlyAt75Percent_ReturnsFalse()
    {
        ResourceAllocator allocator = new(MakeGpuCaps(), NullMonitor);

        List<ExecutionGroup> groups = MakeGroups(1, true);

        bool result = allocator.CheckMemoryCeiling(groups, 267);

        result.Should().BeFalse();
    }

    [Fact]
    public void CheckMemoryCeiling_ZeroEncodeNodes_ReturnsTrue()
    {
        ResourceAllocator allocator = new(CpuOnly, NullMonitor);

        List<ExecutionGroup> groups =
        [
            new(
                "group_0",
                [new("sub_0", OperationType.SubtitleExtract, [], new())],
                null,
                0,
                1,
                false,
                0
            ),
        ];

        bool result = allocator.CheckMemoryCeiling(groups, 256);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AllocateResources_DoesNotThrowForGpuGroups()
    {
        ResourceAllocator allocator = new(MakeGpuCaps(), NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(2, true);

        Func<Task> act = async () => await allocator.AllocateResourcesAsync(groups);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AllocateResources_DoesNotThrowForCpuGroups()
    {
        ResourceAllocator allocator = new(CpuOnly, NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(1, false);

        Func<Task> act = async () => await allocator.AllocateResourcesAsync(groups);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AllocateResources_DoesNotThrowForEmptyGroupList()
    {
        ResourceAllocator allocator = new(MakeGpuCaps(), NullMonitor);

        Func<Task> act = async () => await allocator.AllocateResourcesAsync([]);

        await act.Should().NotThrowAsync();
    }

    // ------------------------------------------------------------------
    // AllocateResources behavior tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task AllocateResources_GpuGroup_AssignsDeviceIdFromHardware()
    {
        ResourceAllocator allocator = new(MakeGpuCaps(), NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(1, true);

        await allocator.AllocateResourcesAsync(groups);

        groups[0].DeviceId.Should().Be("RTX 4090");
    }

    [Fact]
    public async Task AllocateResources_CpuGroup_WithZeroThreads_SetsSoftwareBudget()
    {
        ResourceAllocator allocator = new(CpuOnly, NullMonitor);
        List<ExecutionGroup> groups =
        [
            new(
                "group_0",
                [new("encode_0", OperationType.Encode, [], new())],
                null,
                0,
                0,
                false,
                1
            ),
        ];

        await allocator.AllocateResourcesAsync(groups);

        groups[0].CpuThreadsRequired.Should().BeGreaterThan(0);
        groups[0].CpuThreadsRequired.Should().BeLessThanOrEqualTo(Environment.ProcessorCount);
    }

    [Fact]
    public async Task AllocateResources_CpuGroup_WithExistingThreadCount_DoesNotOverwrite()
    {
        ResourceAllocator allocator = new(CpuOnly, NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(1, false);

        await allocator.AllocateResourcesAsync(groups);

        groups[0].CpuThreadsRequired.Should().Be(4);
    }

    [Fact]
    public async Task AllocateResources_PicksLeastLoadedGpu_FromSampleData()
    {
        IHardwareCapabilities twoGpuHardware = new HardwareCapabilities(
            [
                new(GpuVendor.Nvidia, "GPU-0", 8192, 3, [VideoCodecType.H264]),
                new(GpuVendor.Nvidia, "GPU-1", 8192, 3, [VideoCodecType.H264]),
            ],
            8
        );

        IResourceMonitor loadedGpu0Monitor = new FixedGpuSampleMonitor([
            new(
                100,
                0,
                80,
                0
            ),
            new(
                101,
                1,
                20,
                0
            ),
        ]);

        ResourceAllocator allocator = new(twoGpuHardware, loadedGpu0Monitor);
        List<ExecutionGroup> groups =
        [
            new(
                "group_0",
                [new("encode_0", OperationType.Encode, [], new())],
                null,
                1,
                0,
                true,
                1
            ),
        ];

        await allocator.AllocateResourcesAsync(groups);

        groups[0].DeviceId.Should().Be("GPU-1");
    }
}

internal sealed class FixedGpuSampleMonitor(IReadOnlyList<GpuProcessSample> samples)
    : IResourceMonitor
{
    public double GetCpuUsagePercent() => 0;

    public double GetSystemCpuUsagePercent() => 0;

    public double GetGpuEncodeUtilization(string gpuDeviceKey) => 0;

    public long GetAvailableMemoryMb() => 0;

    public Task<IReadOnlyList<GpuProcessSample>> SampleGpuAsync(
        CancellationToken cancellationToken = default
    ) => Task.FromResult(samples);
}
