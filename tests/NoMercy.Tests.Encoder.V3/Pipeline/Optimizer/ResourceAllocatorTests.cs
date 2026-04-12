namespace NoMercy.Tests.Encoder.V3.Pipeline.Optimizer;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Pipeline.Optimizer;

public class ResourceAllocatorTests
{
    private static IHardwareCapabilities MakeGpuCaps() =>
        new HardwareCapabilities(
            [
                new GpuDevice(
                    GpuVendor.Nvidia,
                    "RTX 4090",
                    24576,
                    12,
                    [VideoCodecType.H264, VideoCodecType.H265]
                ),
            ],
            CpuCores: 16
        );

    private static IHardwareCapabilities CpuOnly => new HardwareCapabilities([], CpuCores: 8);

    private static List<ExecutionGroup> MakeGroups(int encodeCount, bool requiresGpu)
    {
        ExecutionNode[] nodes =
        [
            new ExecutionNode("node_0", OperationType.Decode, [], new Dictionary<string, string>()),
            new ExecutionNode(
                "node_1",
                OperationType.Encode,
                ["node_0"],
                new Dictionary<string, string>()
            ),
        ];

        // Build a group with `encodeCount` Encode nodes
        List<ExecutionNode> allNodes = [nodes[0]];
        for (int i = 0; i < encodeCount; i++)
        {
            allNodes.Add(
                new ExecutionNode(
                    $"encode_{i}",
                    OperationType.Encode,
                    ["node_0"],
                    new Dictionary<string, string>()
                )
            );
        }

        return
        [
            new ExecutionGroup(
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
        IHardwareCapabilities hardware = MakeGpuCaps();
        ResourceAllocator allocator = new(hardware);

        // 1 encode stream * 200MB = 200MB peak, available = 8192MB → well under 75%
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: true);

        bool result = allocator.CheckMemoryCeiling(groups, availableMemoryMb: 8192);

        result.Should().BeTrue();
    }

    [Fact]
    public void CheckMemoryCeiling_WhenOver75Percent_ReturnsFalse()
    {
        IHardwareCapabilities hardware = MakeGpuCaps();
        ResourceAllocator allocator = new(hardware);

        // 100 encode streams * 200MB = 20000MB peak, available = 1024MB → 75% = 768MB → over
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 100, requiresGpu: true);

        bool result = allocator.CheckMemoryCeiling(groups, availableMemoryMb: 1024);

        result.Should().BeFalse();
    }

    [Fact]
    public void CheckMemoryCeiling_ExactlyAt75Percent_ReturnsFalse()
    {
        IHardwareCapabilities hardware = MakeGpuCaps();
        ResourceAllocator allocator = new(hardware);

        // 1 encode * 200MB = 200MB. Available = 267MB → 75% = 200MB. 200 < 200 is false → returns false
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: true);

        bool result = allocator.CheckMemoryCeiling(groups, availableMemoryMb: 267);

        result.Should().BeFalse();
    }

    [Fact]
    public void CheckMemoryCeiling_ZeroEncodeNodes_ReturnsTrue()
    {
        IHardwareCapabilities hardware = CpuOnly;
        ResourceAllocator allocator = new(hardware);

        List<ExecutionGroup> groups =
        [
            new ExecutionGroup(
                GroupId: "group_0",
                Nodes:
                [
                    new ExecutionNode(
                        "sub_0",
                        OperationType.SubtitleExtract,
                        [],
                        new Dictionary<string, string>()
                    ),
                ],
                DeviceId: null,
                GpuSlotsRequired: 0,
                CpuThreadsRequired: 1,
                RequiresGpu: false,
                Priority: 0
            ),
        ];

        bool result = allocator.CheckMemoryCeiling(groups, availableMemoryMb: 256);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllocateResources_DoesNotThrowForGpuGroups()
    {
        IHardwareCapabilities hardware = MakeGpuCaps();
        ResourceAllocator allocator = new(hardware);
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 2, requiresGpu: true);

        Action act = () => allocator.AllocateResources(groups);

        act.Should().NotThrow();
    }

    [Fact]
    public void AllocateResources_DoesNotThrowForCpuGroups()
    {
        IHardwareCapabilities hardware = CpuOnly;
        ResourceAllocator allocator = new(hardware);
        List<ExecutionGroup> groups = MakeGroups(encodeCount: 1, requiresGpu: false);

        Action act = () => allocator.AllocateResources(groups);

        act.Should().NotThrow();
    }

    [Fact]
    public void AllocateResources_DoesNotThrowForEmptyGroupList()
    {
        IHardwareCapabilities hardware = MakeGpuCaps();
        ResourceAllocator allocator = new(hardware);

        Action act = () => allocator.AllocateResources([]);

        act.Should().NotThrow();
    }
}
