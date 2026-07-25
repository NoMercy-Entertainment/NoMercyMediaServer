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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Strategies.Shared;
using NoMercy.Storage;

namespace NoMercy.Encoder.Strategies.Dash;

/// <summary>
/// DASH single-pass output. Delegates to the shared pipeline — the
/// <see cref="NoMercy.Encoder.Output.DashOutputStrategy"/> emits a segmented
/// DASH output with an MPD manifest. Required target for Widevine/PlayReady
/// CENC DRM in a future Phase 11 strategy.
///
/// <see cref="Decompose"/> splits like HLS single-pass: one Video task per
/// representation, one Audio per group, one Subtitle per track.
/// </summary>
public class DashSinglePassStrategy(
    IEncoder encoder,
    ILogger<DashSinglePassStrategy> logger,
    IStorage storage
) : SinglePassStrategyBase(encoder, logger, storage)
{
    public override OutputFormat Format => OutputFormat.Dash;

    public override DecomposedTask[] Decompose(OutputPlan plan, string groupTag)
    {
        List<DecomposedTask> tasks = [];

        for (int i = 0; i < plan.VideoOutputs.Length; i++)
        {
            VideoOutputPlan video = plan.VideoOutputs[i];
            tasks.Add(
                new(
                    $"{groupTag}-video-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Video,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.ForVideoOutput(video),
                    EstimatedCostUnits: EstimateVideoCost(video),
                    Label: $"{video.Width}p {video.EncoderName}"
                )
            );
        }

        for (int i = 0; i < plan.AudioOutputs.Length; i++)
        {
            AudioOutputPlan audio = plan.AudioOutputs[i];
            tasks.Add(
                new(
                    $"{groupTag}-audio-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Audio,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.CpuOnly(1),
                    EstimatedCostUnits: 1,
                    Label: $"{audio.Language ?? "und"} {audio.EncoderName}"
                )
            );
        }

        for (int i = 0; i < plan.SubtitleOutputs.Length; i++)
        {
            SubtitleOutputPlan sub = plan.SubtitleOutputs[i];
            tasks.Add(
                new(
                    $"{groupTag}-sub-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Subtitle,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.CpuOnly(1),
                    EstimatedCostUnits: 1,
                    Label: $"sub {sub.Language ?? "und"}"
                )
            );
        }

        if (plan is { GenerateChapterThumbs: true, Chapters.Count: > 0 })
        {
            int count = plan.Chapters.Count;
            for (int i = 0; i < count; i++)
            {
                ChapterInfo chapter = plan.Chapters[i];
                tasks.Add(
                    new(
                        $"{groupTag}-chapter-{i}",
                        ParentJobId: 0,
                        GroupTag: groupTag,
                        Kind: EncodeTaskKind.Chapters,
                        OutputIndex: i,
                        Resources: TaskResourceHelper.CpuOnly(1),
                        EstimatedCostUnits: 1,
                        Label: $"chapter still {i + 1}/{count} @ {chapter.Start.TotalSeconds:F0}s"
                    )
                );
            }
        }

        if (tasks.Count == 0)
            return [IEncodingStrategy.WholeTask(groupTag)];

        return tasks.ToArray();
    }

    private static int EstimateVideoCost(VideoOutputPlan video)
    {
        if (video.Width >= 3840)
            return 8;
        if (video.Width >= 1920)
            return 4;
        if (video.Width >= 1280)
            return 2;
        return 1;
    }
}
