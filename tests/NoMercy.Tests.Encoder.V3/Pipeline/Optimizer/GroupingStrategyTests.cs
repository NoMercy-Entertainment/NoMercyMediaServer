namespace NoMercy.Tests.Encoder.V3.Pipeline.Optimizer;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Pipeline.Optimizer;

public class GroupingStrategyTests
{
    private static readonly GroupingStrategy Strategy = new();

    // ------------------------------------------------------------------
    // Hardware helpers
    // ------------------------------------------------------------------

    private static IHardwareCapabilities MakeGpuCaps(int maxSessions) =>
        new HardwareCapabilities(
            [
                new GpuDevice(
                    GpuVendor.Nvidia,
                    "RTX 4090",
                    24576,
                    maxSessions,
                    [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
                ),
            ],
            CpuCores: 16
        );

    private static IHardwareCapabilities NoCpuOnly => new HardwareCapabilities([], CpuCores: 8);

    // ------------------------------------------------------------------
    // Node factory helpers
    // ------------------------------------------------------------------

    private static List<ExecutionNode> BuildVideoChainNodes(
        int encodeCount,
        string decodeId = "node_0"
    )
    {
        List<ExecutionNode> nodes =
        [
            new ExecutionNode(
                decodeId,
                OperationType.Decode,
                [],
                new Dictionary<string, string> { ["stream_index"] = "0", ["codec"] = "h264" }
            ),
        ];

        for (int i = 0; i < encodeCount; i++)
        {
            string scaleId = $"node_scale_{i}";
            nodes.Add(
                new ExecutionNode(
                    scaleId,
                    OperationType.Scale,
                    [decodeId],
                    new Dictionary<string, string>
                    {
                        ["width"] = "1920",
                        ["height"] = "1080",
                        ["split_index"] = i.ToString(),
                    }
                )
            );

            nodes.Add(
                new ExecutionNode(
                    $"node_encode_{i}",
                    OperationType.Encode,
                    [scaleId],
                    new Dictionary<string, string>
                    {
                        ["encoder"] = "h264_nvenc",
                        ["crf"] = "22",
                        ["preset"] = "fast",
                        ["width"] = "1920",
                        ["height"] = "1080",
                    }
                )
            );
        }

        return nodes;
    }

    private static List<ExecutionNode> SubtitleNodes(int count)
    {
        List<ExecutionNode> nodes = [];
        for (int i = 0; i < count; i++)
        {
            nodes.Add(
                new ExecutionNode(
                    $"sub_{i}",
                    OperationType.SubtitleExtract,
                    [],
                    new Dictionary<string, string>
                    {
                        ["stream_index"] = i.ToString(),
                        ["language"] = "eng",
                    }
                )
            );
        }

        return nodes;
    }

    private static List<ExecutionNode> ThumbnailNodes() =>
        [
            new ExecutionNode(
                "thumb_0",
                OperationType.ThumbnailCapture,
                [],
                new Dictionary<string, string> { ["width"] = "320", ["interval"] = "10" }
            ),
        ];

    private static List<ExecutionNode> ChapterNodes() =>
        [
            new ExecutionNode(
                "chapter_0",
                OperationType.ChapterExtract,
                [],
                new Dictionary<string, string>()
            ),
        ];

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact]
    public void ThreeOutputsOnGpuWith12SessionLimit_FitsInOneGroup()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(3);
        IHardwareCapabilities hardware = MakeGpuCaps(12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        ExecutionGroup mainGroup = groups.Single(g => g.RequiresGpu);
        mainGroup.GpuSlotsRequired.Should().Be(3);
        mainGroup.Nodes.Count(n => n.Operation == OperationType.Encode).Should().Be(3);
    }

    [Fact]
    public void FifteenOutputsWithThreeSessionLimit_SplitsIntoMultipleGroups()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(15);
        IHardwareCapabilities hardware = MakeGpuCaps(3);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        IEnumerable<ExecutionGroup> gpuGroups = groups.Where(g => g.RequiresGpu);
        gpuGroups.Should().HaveCountGreaterThan(1);

        // Total encode nodes across all GPU groups must equal 15
        int totalEncodes = gpuGroups.Sum(g =>
            g.Nodes.Count(n => n.Operation == OperationType.Encode)
        );
        totalEncodes.Should().Be(15);
    }

    [Fact]
    public void FifteenOutputsWithThreeSessionLimit_EachGroupHasAtMostThreeEncodes()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(15);
        IHardwareCapabilities hardware = MakeGpuCaps(3);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        foreach (ExecutionGroup group in groups.Where(g => g.RequiresGpu))
        {
            group
                .Nodes.Count(n => n.Operation == OperationType.Encode)
                .Should()
                .BeLessThanOrEqualTo(3);
        }
    }

    [Fact]
    public void SubtitleNodes_FormSeparateGroupWithPriorityZero()
    {
        List<ExecutionNode> nodes = [.. BuildVideoChainNodes(1), .. SubtitleNodes(2)];
        IHardwareCapabilities hardware = MakeGpuCaps(12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        ExecutionGroup subGroup = groups.Single(g =>
            g.Nodes.Any(n => n.Operation == OperationType.SubtitleExtract)
        );
        subGroup.RequiresGpu.Should().BeFalse();
        subGroup.Priority.Should().Be(0);
    }

    [Fact]
    public void ThumbnailNodes_FormSeparateGroupWithPriorityTwo()
    {
        List<ExecutionNode> nodes = [.. BuildVideoChainNodes(1), .. ThumbnailNodes()];
        IHardwareCapabilities hardware = MakeGpuCaps(12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        ExecutionGroup thumbGroup = groups.Single(g =>
            g.Nodes.Any(n => n.Operation == OperationType.ThumbnailCapture)
        );
        thumbGroup.RequiresGpu.Should().BeFalse();
        thumbGroup.Priority.Should().Be(2);
    }

    [Fact]
    public void ChapterNodes_FormSeparateGroupWithPriorityZero()
    {
        List<ExecutionNode> nodes = [.. BuildVideoChainNodes(1), .. ChapterNodes()];
        IHardwareCapabilities hardware = MakeGpuCaps(12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        ExecutionGroup chapterGroup = groups.Single(g =>
            g.Nodes.Any(n => n.Operation == OperationType.ChapterExtract)
        );
        chapterGroup.Priority.Should().Be(0);
    }

    [Fact]
    public void Groups_AreOrderedByPriorityAscending()
    {
        List<ExecutionNode> nodes =
        [
            .. BuildVideoChainNodes(1),
            .. SubtitleNodes(1),
            .. ThumbnailNodes(),
            .. ChapterNodes(),
        ];
        IHardwareCapabilities hardware = MakeGpuCaps(12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        IEnumerable<int> priorities = groups.Select(g => g.Priority);
        priorities.Should().BeInAscendingOrder();
    }

    [Fact]
    public void CpuOnlyHardware_MainGroupDoesNotRequireGpu()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(1);
        IHardwareCapabilities hardware = NoCpuOnly;

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        ExecutionGroup mainGroup = groups.Single(g =>
            g.Nodes.Any(n => n.Operation == OperationType.Encode)
        );
        mainGroup.RequiresGpu.Should().BeFalse();
        mainGroup.DeviceId.Should().BeNull();
    }

    [Fact]
    public void GpuHardware_MainGroupHasDeviceId()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(1);
        IHardwareCapabilities hardware = MakeGpuCaps(12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        ExecutionGroup mainGroup = groups.Single(g => g.RequiresGpu);
        mainGroup.DeviceId.Should().Be("RTX 4090");
    }

    [Fact]
    public void AllGroups_HaveUniqueGroupIds()
    {
        List<ExecutionNode> nodes =
        [
            .. BuildVideoChainNodes(3),
            .. SubtitleNodes(2),
            .. ThumbnailNodes(),
            .. ChapterNodes(),
        ];
        IHardwareCapabilities hardware = MakeGpuCaps(12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes, hardware);

        IEnumerable<string> groupIds = groups.Select(g => g.GroupId);
        groupIds.Should().OnlyHaveUniqueItems();
    }
}
