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

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class GroupingStrategyTests
{
    private static readonly GroupingStrategy Strategy = new();

    // ------------------------------------------------------------------
    // Hardware helpers
    // ------------------------------------------------------------------

    private static IHardwareCapabilities MakeGpuCaps(int maxSessions) =>
        new HardwareCapabilities(
            Gpus:
            [
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 4090",
                    VramMb: 24576,
                    MaxEncoderSessions: maxSessions,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
                ),
            ],
            CpuCores: 16
        );

    private static IHardwareCapabilities NoCpuOnly => new HardwareCapabilities(Gpus: [], CpuCores: 8);

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
            new(
                Id: decodeId,
                Operation: OperationType.Decode,
                DependsOn: [],
                Parameters: new() { [key: "stream_index"] = "0", [key: "codec"] = "h264" }
            ),
        ];

        for (int i = 0; i < encodeCount; i++)
        {
            string scaleId = $"node_scale_{i}";
            nodes.Add(
                item: new(
                    Id: scaleId,
                    Operation: OperationType.Scale,
                    DependsOn: [decodeId],
                    Parameters: new()
                    {
                        [key: "width"] = "1920",
                        [key: "height"] = "1080",
                        [key: "split_index"] = i.ToString(),
                    }
                )
            );

            nodes.Add(
                item: new(
                    Id: $"node_encode_{i}",
                    Operation: OperationType.Encode,
                    DependsOn: [scaleId],
                    Parameters: new()
                    {
                        [key: "encoder"] = "h264_nvenc",
                        [key: "crf"] = "22",
                        [key: "preset"] = "fast",
                        [key: "width"] = "1920",
                        [key: "height"] = "1080",
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
                item: new(
                    Id: $"sub_{i}",
                    Operation: OperationType.SubtitleExtract,
                    DependsOn: [],
                    Parameters: new() { [key: "stream_index"] = i.ToString(), [key: "language"] = "eng" }
                )
            );
        }

        return nodes;
    }

    private static List<ExecutionNode> ThumbnailNodes() =>
        [
            new(
                Id: "thumb_0",
                Operation: OperationType.ThumbnailCapture,
                DependsOn: [],
                Parameters: new() { [key: "width"] = "320", [key: "interval"] = "10" }
            ),
        ];

    private static List<ExecutionNode> ChapterNodes() =>
        [new(Id: "chapter_0", Operation: OperationType.ChapterExtract, DependsOn: [], Parameters: new())];

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact]
    public void ThreeOutputsOnGpuWith12SessionLimit_FitsInOneGroup()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(encodeCount: 3);
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        ExecutionGroup mainGroup = groups.Single(predicate: g => g.RequiresGpu);
        mainGroup.GpuSlotsRequired.Should().Be(expected: 3);
        mainGroup.Nodes.Count(predicate: n => n.Operation == OperationType.Encode).Should().Be(expected: 3);
    }

    [Fact]
    public void FifteenOutputsWithThreeSessionLimit_SplitsIntoMultipleGroups()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(encodeCount: 15);
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 3);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        IEnumerable<ExecutionGroup> gpuGroups = groups.Where(predicate: g => g.RequiresGpu);
        gpuGroups.Should().HaveCountGreaterThan(expected: 1);

        // Total encode nodes across all GPU groups must equal 15
        int totalEncodes = gpuGroups.Sum(selector: g =>
            g.Nodes.Count(predicate: n => n.Operation == OperationType.Encode)
        );
        totalEncodes.Should().Be(expected: 15);
    }

    [Fact]
    public void FifteenOutputsWithThreeSessionLimit_EachGroupHasAtMostThreeEncodes()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(encodeCount: 15);
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 3);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        foreach (ExecutionGroup group in groups.Where(predicate: g => g.RequiresGpu))
        {
            group
                .Nodes.Count(predicate: n => n.Operation == OperationType.Encode)
                .Should()
                .BeLessThanOrEqualTo(expected: 3);
        }
    }

    [Fact]
    public void SubtitleNodes_FormSeparateGroupWithPriorityZero()
    {
        List<ExecutionNode> nodes = [.. BuildVideoChainNodes(encodeCount: 1), .. SubtitleNodes(count: 2)];
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        ExecutionGroup subGroup = groups.Single(predicate: g =>
            g.Nodes.Any(predicate: n => n.Operation == OperationType.SubtitleExtract)
        );
        subGroup.RequiresGpu.Should().BeFalse();
        subGroup.Priority.Should().Be(expected: 0);
    }

    [Fact]
    public void ThumbnailNodes_FormSeparateGroupWithPriorityTwo()
    {
        List<ExecutionNode> nodes = [.. BuildVideoChainNodes(encodeCount: 1), .. ThumbnailNodes()];
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        ExecutionGroup thumbGroup = groups.Single(predicate: g =>
            g.Nodes.Any(predicate: n => n.Operation == OperationType.ThumbnailCapture)
        );
        thumbGroup.RequiresGpu.Should().BeFalse();
        thumbGroup.Priority.Should().Be(expected: 2);
    }

    [Fact]
    public void ChapterNodes_FormSeparateGroupWithPriorityZero()
    {
        List<ExecutionNode> nodes = [.. BuildVideoChainNodes(encodeCount: 1), .. ChapterNodes()];
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        ExecutionGroup chapterGroup = groups.Single(predicate: g =>
            g.Nodes.Any(predicate: n => n.Operation == OperationType.ChapterExtract)
        );
        chapterGroup.Priority.Should().Be(expected: 0);
    }

    [Fact]
    public void Groups_AreOrderedByPriorityAscending()
    {
        List<ExecutionNode> nodes =
        [
            .. BuildVideoChainNodes(encodeCount: 1),
            .. SubtitleNodes(count: 1),
            .. ThumbnailNodes(),
            .. ChapterNodes(),
        ];
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        IEnumerable<int> priorities = groups.Select(selector: g => g.Priority);
        priorities.Should().BeInAscendingOrder();
    }

    [Fact]
    public void CpuOnlyHardware_MainGroupDoesNotRequireGpu()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(encodeCount: 1);
        IHardwareCapabilities hardware = NoCpuOnly;

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        ExecutionGroup mainGroup = groups.Single(predicate: g =>
            g.Nodes.Any(predicate: n => n.Operation == OperationType.Encode)
        );
        mainGroup.RequiresGpu.Should().BeFalse();
        mainGroup.DeviceId.Should().BeNull();
    }

    [Fact]
    public void GpuHardware_MainGroupHasDeviceId()
    {
        List<ExecutionNode> nodes = BuildVideoChainNodes(encodeCount: 1);
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        ExecutionGroup mainGroup = groups.Single(predicate: g => g.RequiresGpu);
        mainGroup.DeviceId.Should().Be(expected: "RTX 4090");
    }

    [Fact]
    public void AllGroups_HaveUniqueGroupIds()
    {
        List<ExecutionNode> nodes =
        [
            .. BuildVideoChainNodes(encodeCount: 3),
            .. SubtitleNodes(count: 2),
            .. ThumbnailNodes(),
            .. ChapterNodes(),
        ];
        IHardwareCapabilities hardware = MakeGpuCaps(maxSessions: 12);

        List<ExecutionGroup> groups = Strategy.GroupNodes(nodes: nodes, hardware: hardware);

        IEnumerable<string> groupIds = groups.Select(selector: g => g.GroupId);
        groupIds.Should().OnlyHaveUniqueItems();
    }
}
