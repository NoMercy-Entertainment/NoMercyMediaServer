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

using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Pipeline.Optimizer;

public class GroupingStrategy
{
    public List<ExecutionGroup> GroupNodes(
        List<ExecutionNode> nodes,
        IHardwareCapabilities hardware
    )
    {
        List<ExecutionGroup> groups = [];
        int groupId = 0;

        // Partition nodes by category
        List<ExecutionNode> videoChain = nodes.Where(predicate: n => IsVideoOperation(op: n.Operation)).ToList();

        List<ExecutionNode> audioNodes = nodes
            .Where(predicate: n =>
                n.Operation
                    is OperationType.AudioDecode
                        or OperationType.AudioEncode
                        or OperationType.AudioResample
            )
            .ToList();

        List<ExecutionNode> subtitleNodes = nodes
            .Where(predicate: n =>
                n.Operation is OperationType.SubtitleExtract or OperationType.SubtitleConvert
            )
            .ToList();

        List<ExecutionNode> chapterNodes = nodes
            .Where(predicate: n => n.Operation is OperationType.ChapterExtract or OperationType.FontExtract)
            .ToList();

        List<ExecutionNode> thumbnailNodes = nodes
            .Where(predicate: n =>
                n.Operation is OperationType.ThumbnailCapture or OperationType.SpriteAssemble
            )
            .ToList();

        // Main group: video + audio (share FFmpeg invocation)
        if (videoChain.Count > 0 || audioNodes.Count > 0)
        {
            int encodeCount = videoChain.Count(predicate: n => n.Operation == OperationType.Encode);
            int maxSessions = hardware.HasGpu ? hardware.Gpus[index: 0].MaxEncoderSessions : int.MaxValue;

            if (encodeCount <= maxSessions)
            {
                // All fit in one group
                List<ExecutionNode> mainNodes = [.. videoChain, .. audioNodes];
                groups.Add(
                    item: new(
                        GroupId: $"group_{groupId++}",
                        Nodes: mainNodes.ToArray(),
                        DeviceId: hardware.HasGpu ? hardware.Gpus[index: 0].Name : null,
                        GpuSlotsRequired: hardware.HasGpu ? encodeCount : 0,
                        CpuThreadsRequired: hardware.HasGpu ? 0 : 4,
                        RequiresGpu: hardware.HasGpu,
                        Priority: 1
                    )
                );
            }
            else
            {
                // Split encode nodes into batches of maxSessions
                List<ExecutionNode> sharedNodes = videoChain
                    .Where(predicate: n => n.Operation is not OperationType.Encode)
                    .ToList();

                List<ExecutionNode> encodeNodes = videoChain
                    .Where(predicate: n => n.Operation == OperationType.Encode)
                    .ToList();

                for (int i = 0; i < encodeNodes.Count; i += maxSessions)
                {
                    List<ExecutionNode> batch = encodeNodes.Skip(count: i).Take(count: maxSessions).ToList();
                    List<ExecutionNode> groupNodes =
                        i == 0 ? [.. sharedNodes, .. batch, .. audioNodes] : [.. batch];

                    groups.Add(
                        item: new(
                            GroupId: $"group_{groupId++}",
                            Nodes: groupNodes.ToArray(),
                            DeviceId: hardware.HasGpu ? hardware.Gpus[index: 0].Name : null,
                            GpuSlotsRequired: batch.Count,
                            CpuThreadsRequired: 0,
                            RequiresGpu: hardware.HasGpu,
                            Priority: 1
                        )
                    );
                }
            }
        }

        // Independent groups — priority 0 (run first)
        if (subtitleNodes.Count > 0)
        {
            groups.Add(
                item: new(
                    GroupId: $"group_{groupId++}",
                    Nodes: subtitleNodes.ToArray(),
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 1,
                    RequiresGpu: false,
                    Priority: 0
                )
            );
        }

        if (chapterNodes.Count > 0)
        {
            groups.Add(
                item: new(
                    GroupId: $"group_{groupId++}",
                    Nodes: chapterNodes.ToArray(),
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 1,
                    RequiresGpu: false,
                    Priority: 0
                )
            );
        }

        if (thumbnailNodes.Count > 0)
        {
            groups.Add(
                item: new(
                    GroupId: $"group_{groupId++}",
                    Nodes: thumbnailNodes.ToArray(),
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 1,
                    RequiresGpu: false,
                    Priority: 2
                )
            );
        }

        return groups.OrderBy(keySelector: g => g.Priority).ToList();
    }

    private static bool IsVideoOperation(OperationType op) =>
        op
            is OperationType.Decode
                or OperationType.HwUpload
                or OperationType.HwDownload
                or OperationType.Tonemap
                or OperationType.Deinterlace
                or OperationType.Scale
                or OperationType.Crop
                or OperationType.Split
                or OperationType.Encode;
}
