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
        List<ExecutionNode> videoChain = nodes.Where(n => IsVideoOperation(n.Operation)).ToList();

        List<ExecutionNode> audioNodes = nodes
            .Where(n =>
                n.Operation
                    is OperationType.AudioDecode
                        or OperationType.AudioEncode
                        or OperationType.AudioResample
            )
            .ToList();

        List<ExecutionNode> subtitleNodes = nodes
            .Where(n =>
                n.Operation is OperationType.SubtitleExtract or OperationType.SubtitleConvert
            )
            .ToList();

        List<ExecutionNode> chapterNodes = nodes
            .Where(n => n.Operation is OperationType.ChapterExtract or OperationType.FontExtract)
            .ToList();

        List<ExecutionNode> thumbnailNodes = nodes
            .Where(n =>
                n.Operation is OperationType.ThumbnailCapture or OperationType.SpriteAssemble
            )
            .ToList();

        // Main group: video + audio (share FFmpeg invocation)
        if (videoChain.Count > 0 || audioNodes.Count > 0)
        {
            int encodeCount = videoChain.Count(n => n.Operation == OperationType.Encode);
            int maxSessions = hardware.HasGpu ? hardware.Gpus[0].MaxEncoderSessions : int.MaxValue;

            if (encodeCount <= maxSessions)
            {
                // All fit in one group
                List<ExecutionNode> mainNodes = [.. videoChain, .. audioNodes];
                groups.Add(
                    new(
                        $"group_{groupId++}",
                        mainNodes.ToArray(),
                        hardware.HasGpu ? hardware.Gpus[0].Name : null,
                        hardware.HasGpu ? encodeCount : 0,
                        hardware.HasGpu ? 0 : 4,
                        hardware.HasGpu,
                        1
                    )
                );
            }
            else
            {
                // Split encode nodes into batches of maxSessions
                List<ExecutionNode> sharedNodes = videoChain
                    .Where(n => n.Operation is not OperationType.Encode)
                    .ToList();

                List<ExecutionNode> encodeNodes = videoChain
                    .Where(n => n.Operation == OperationType.Encode)
                    .ToList();

                for (int i = 0; i < encodeNodes.Count; i += maxSessions)
                {
                    List<ExecutionNode> batch = encodeNodes.Skip(i).Take(maxSessions).ToList();
                    List<ExecutionNode> groupNodes =
                        i == 0 ? [.. sharedNodes, .. batch, .. audioNodes] : [.. batch];

                    groups.Add(
                        new(
                            $"group_{groupId++}",
                            groupNodes.ToArray(),
                            hardware.HasGpu ? hardware.Gpus[0].Name : null,
                            batch.Count,
                            0,
                            hardware.HasGpu,
                            1
                        )
                    );
                }
            }
        }

        // Independent groups — priority 0 (run first)
        if (subtitleNodes.Count > 0)
        {
            groups.Add(
                new(
                    $"group_{groupId++}",
                    subtitleNodes.ToArray(),
                    null,
                    0,
                    1,
                    false,
                    0
                )
            );
        }

        if (chapterNodes.Count > 0)
        {
            groups.Add(
                new(
                    $"group_{groupId++}",
                    chapterNodes.ToArray(),
                    null,
                    0,
                    1,
                    false,
                    0
                )
            );
        }

        if (thumbnailNodes.Count > 0)
        {
            groups.Add(
                new(
                    $"group_{groupId++}",
                    thumbnailNodes.ToArray(),
                    null,
                    0,
                    1,
                    false,
                    2
                )
            );
        }

        return groups.OrderBy(g => g.Priority).ToList();
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
