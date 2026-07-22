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
            Gpus:
            [
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 4090",
                    VramMb: 24576,
                    MaxEncoderSessions: 12,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
                ),
            ],
            CpuCores: 16
        );

    private static IHardwareCapabilities CpuOnly => new HardwareCapabilities(Gpus: [], CpuCores: 8);

    private static List<ExecutionGroup> MakeGroups(int encodeCount, bool requiresGpu)
    {
        List<ExecutionNode> allNodes = [new(Id: "node_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new())];
        for (int i = 0; i < encodeCount; i++)
        {
            allNodes.Add(item: new(Id: $"encode_{i}", Operation: OperationType.Encode, DependsOn: ["node_0"], Parameters: new()));
        }

        return
        [
            new(
                GroupId: "group_0",
                Nodes: allNodes.ToArray(),
                DeviceId: requiresGpu ? "RTX 4090" : null,
                GpuSlotsRequired: requiresGpu ? encodeCount : 0,
                CpuThreadsRequired: requiresGpu ? 0 : 4,
                RequiresGpu: requiresGpu,
                Priority: 1
            ),
        ];
    }

    // ------------------------------------------------------------------
    // Memory ceiling tests
    // ------------------------------------------------------------------

    [Fact]
    public void CheckMemoryCeiling_WhenUnder75Percent_ReturnsTrue()
    {
        ResourceAllocator allocator = new(hardware: MakeGpuCaps(), monitor: NullMonitor);

        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: true);

        bool result = allocator.CheckMemoryCeiling(groups: groups, availableMemoryMb: 8192);

        result.Should().BeTrue();
    }

    [Fact]
    public void CheckMemoryCeiling_WhenOver75Percent_ReturnsFalse()
    {
        ResourceAllocator allocator = new(hardware: MakeGpuCaps(), monitor: NullMonitor);

        List<ExecutionGroup> groups = MakeGroups(encodeCount: 100, requiresGpu: true);

        bool result = allocator.CheckMemoryCeiling(groups: groups, availableMemoryMb: 1024);

        result.Should().BeFalse();
    }

    [Fact]
    public void CheckMemoryCeiling_ExactlyAt75Percent_ReturnsFalse()
    {
        ResourceAllocator allocator = new(hardware: MakeGpuCaps(), monitor: NullMonitor);

        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: true);

        bool result = allocator.CheckMemoryCeiling(groups: groups, availableMemoryMb: 267);

        result.Should().BeFalse();
    }

    [Fact]
    public void CheckMemoryCeiling_ZeroEncodeNodes_ReturnsTrue()
    {
        ResourceAllocator allocator = new(hardware: CpuOnly, monitor: NullMonitor);

        List<ExecutionGroup> groups =
        [
            new(
                GroupId: "group_0",
                Nodes: [new(Id: "sub_0", Operation: OperationType.SubtitleExtract, DependsOn: [], Parameters: new())],
                DeviceId: null,
                GpuSlotsRequired: 0,
                CpuThreadsRequired: 1,
                RequiresGpu: false,
                Priority: 0
            ),
        ];

        bool result = allocator.CheckMemoryCeiling(groups: groups, availableMemoryMb: 256);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AllocateResources_DoesNotThrowForGpuGroups()
    {
        ResourceAllocator allocator = new(hardware: MakeGpuCaps(), monitor: NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 2, requiresGpu: true);

        Func<Task> act = async () => await allocator.AllocateResourcesAsync(groups: groups);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AllocateResources_DoesNotThrowForCpuGroups()
    {
        ResourceAllocator allocator = new(hardware: CpuOnly, monitor: NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: false);

        Func<Task> act = async () => await allocator.AllocateResourcesAsync(groups: groups);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AllocateResources_DoesNotThrowForEmptyGroupList()
    {
        ResourceAllocator allocator = new(hardware: MakeGpuCaps(), monitor: NullMonitor);

        Func<Task> act = async () => await allocator.AllocateResourcesAsync(groups: []);

        await act.Should().NotThrowAsync();
    }

    // ------------------------------------------------------------------
    // AllocateResources behavior tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task AllocateResources_GpuGroup_AssignsDeviceIdFromHardware()
    {
        ResourceAllocator allocator = new(hardware: MakeGpuCaps(), monitor: NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: true);

        await allocator.AllocateResourcesAsync(groups: groups);

        groups[index: 0].DeviceId.Should().Be(expected: "RTX 4090");
    }

    [Fact]
    public async Task AllocateResources_CpuGroup_WithZeroThreads_SetsSoftwareBudget()
    {
        ResourceAllocator allocator = new(hardware: CpuOnly, monitor: NullMonitor);
        List<ExecutionGroup> groups =
        [
            new(
                GroupId: "group_0",
                Nodes: [new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: [], Parameters: new())],
                DeviceId: null,
                GpuSlotsRequired: 0,
                CpuThreadsRequired: 0,
                RequiresGpu: false,
                Priority: 1
            ),
        ];

        await allocator.AllocateResourcesAsync(groups: groups);

        groups[index: 0].CpuThreadsRequired.Should().BeGreaterThan(expected: 0);
        groups[index: 0].CpuThreadsRequired.Should().BeLessThanOrEqualTo(expected: Environment.ProcessorCount);
    }

    [Fact]
    public async Task AllocateResources_CpuGroup_WithExistingThreadCount_DoesNotOverwrite()
    {
        ResourceAllocator allocator = new(hardware: CpuOnly, monitor: NullMonitor);
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: false);

        await allocator.AllocateResourcesAsync(groups: groups);

        groups[index: 0].CpuThreadsRequired.Should().Be(expected: 4);
    }

    [Fact]
    public async Task AllocateResources_PicksLeastLoadedGpu_FromSampleData()
    {
        IHardwareCapabilities twoGpuHardware = new HardwareCapabilities(
            Gpus:
            [
                new(Vendor: GpuVendor.Nvidia, Name: "GPU-0", VramMb: 8192, MaxEncoderSessions: 3, SupportedCodecs: [VideoCodecType.H264]),
                new(Vendor: GpuVendor.Nvidia, Name: "GPU-1", VramMb: 8192, MaxEncoderSessions: 3, SupportedCodecs: [VideoCodecType.H264]),
            ],
            CpuCores: 8
        );

        IResourceMonitor loadedGpu0Monitor = new FixedGpuSampleMonitor(samples:
        [
            new(
                Pid: 100,
                GpuIndex: 0,
                EncoderUtilizationPercent: 80,
                EncoderMemoryBytes: 0
            ),
            new(
                Pid: 101,
                GpuIndex: 1,
                EncoderUtilizationPercent: 20,
                EncoderMemoryBytes: 0
            ),
        ]);

        ResourceAllocator allocator = new(hardware: twoGpuHardware, monitor: loadedGpu0Monitor);
        List<ExecutionGroup> groups =
        [
            new(
                GroupId: "group_0",
                Nodes: [new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: [], Parameters: new())],
                DeviceId: null,
                GpuSlotsRequired: 1,
                CpuThreadsRequired: 0,
                RequiresGpu: true,
                Priority: 1
            ),
        ];

        await allocator.AllocateResourcesAsync(groups: groups);

        groups[index: 0].DeviceId.Should().Be(expected: "GPU-1");
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
    ) => Task.FromResult(result: samples);
}
