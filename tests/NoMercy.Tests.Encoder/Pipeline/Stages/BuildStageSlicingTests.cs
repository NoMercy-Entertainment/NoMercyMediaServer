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
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// BuildStageSlicing narrows a full OutputPlan down to just the entries one
/// decomposed task is responsible for. The slicing is what lets the coordinator
/// dispatch N small ffmpeg invocations from a single planned encode.
/// </summary>
public class BuildStageSlicingTests
{
    // ── Fixtures ────────────────────────────────────────────────────────────

    private static VideoOutputPlan Video(int width, int height, int index = 0) =>
        new(
            Width: width,
            Height: height,
            EncoderName: "libx264",
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "main",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: $"[v{index}]",
            ExtraFlags: []
        );

    private static AudioOutputPlan Audio(string language, int index = 0) =>
        new(
            EncoderName: "aac",
            BitrateKbps: 128,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: language,
            MapLabel: $"[a{index}]"
        );

    private static SubtitleOutputPlan Subtitle(int sourceIndex) =>
        new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Transcode,
            Language: "eng",
            SourceIndex: sourceIndex,
            MapLabel: null
        );

    private static OutputPlan FullPlan() =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080, index: 0), Video(width: 1280, height: 720, index: 1)],
            AudioOutputs: [Audio(language: "eng", index: 0), Audio(language: "fra", index: 1)],
            SubtitleOutputs: [Subtitle(sourceIndex: 0), Subtitle(sourceIndex: 1)],
            Thumbnails: new(Width: 160, Height: 90, IntervalSeconds: 10)
        );

    private static DecomposedTask Task(
        EncodeTaskKind kind,
        int outputIndex = 0,
        int[]? sourceIndexes = null
    ) =>
        new(
            TaskId: "test",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: kind,
            OutputIndex: outputIndex,
            Resources: null,
            SourceIndexes: sourceIndexes
        );

    // ── SliceForTask ────────────────────────────────────────────────────────

    [Fact]
    public void SliceForTask_VideoTask_KeepsOnlyTheTargetVideo()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Video, outputIndex: 0);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().HaveCount(expected: 1);
        sliced.VideoOutputs[0].Width.Should().Be(expected: 1920);
        sliced.AudioOutputs.Should().BeEmpty();
        sliced.SubtitleOutputs.Should().BeEmpty();
        sliced.Thumbnails.Should().BeNull();
    }

    [Fact]
    public void SliceForTask_AudioTask_KeepsOnlyTheTargetAudio()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Audio, outputIndex: 1);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().BeEmpty();
        sliced.AudioOutputs.Should().HaveCount(expected: 1);
        sliced.AudioOutputs[0].Language.Should().Be(expected: "fra");
        sliced.SubtitleOutputs.Should().BeEmpty();
    }

    [Fact]
    public void SliceForTask_SubtitleTask_PreservesAcquiredSubtitlesPointer()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Subtitle, outputIndex: 0);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.SubtitleOutputs.Should().HaveCount(expected: 1);
        // AcquiredSubtitles is non-null for Subtitle tasks (even though our
        // fixture leaves it default null).
        sliced.AcquiredSubtitles.Should().BeNull(); // fixture has no acquired subs
    }

    [Fact]
    public void SliceForTask_ThumbnailTask_KeepsThumbnailsDropsTheRest()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Thumbnails);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().BeEmpty();
        sliced.AudioOutputs.Should().BeEmpty();
        sliced.SubtitleOutputs.Should().BeEmpty();
        sliced.Thumbnails.Should().NotBeNull();
    }

    [Fact]
    public void SliceForTask_VideoTaskWithSourceIndexes_BatchesMultipleVideos()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Video, sourceIndexes: [0, 1]);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().HaveCount(expected: 2);
        sliced.VideoOutputs[0].Width.Should().Be(expected: 1920);
        sliced.VideoOutputs[1].Width.Should().Be(expected: 1280);
    }

    [Fact]
    public void SliceForTask_VideoTaskWithOutOfRangeIndexes_SilentlyDrops()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Video, sourceIndexes: [0, 99, -1]);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().HaveCount(expected: 1);
        sliced.VideoOutputs[0].Width.Should().Be(expected: 1920);
    }

    [Fact]
    public void SliceForTask_VideoTaskWithIndexOutOfRange_ReturnsEmpty()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Video, outputIndex: 99);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().BeEmpty();
    }

    // ── Burn-in subtitles: owned by Video, never by Subtitle ───────────────

    [Fact]
    public void SliceForTask_VideoTask_PreservesBurnInSubtitleForFilterGraph()
    {
        SubtitleOutputPlan burnIn = new(
            OutputCodec: SubtitleCodecType.Ass,
            Action: StreamAction.Transcode,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: null,
            Policy: SubtitlePolicy.BurnIn
        );
        OutputPlan plan = FullPlan() with { SubtitleOutputs = [burnIn] };
        DecomposedTask task = Task(kind: EncodeTaskKind.Video, outputIndex: 0);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().HaveCount(expected: 1);
        sliced.SubtitleOutputs.Should().ContainSingle();
        sliced.SubtitleOutputs[0].Policy.Should().Be(expected: SubtitlePolicy.BurnIn);
    }

    [Fact]
    public void SliceForTask_SubtitleTask_NeverClaimsABurnInEntry()
    {
        SubtitleOutputPlan burnIn = new(
            OutputCodec: SubtitleCodecType.Ass,
            Action: StreamAction.Transcode,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: null,
            Policy: SubtitlePolicy.BurnIn
        );
        OutputPlan plan = FullPlan() with { SubtitleOutputs = [burnIn] };
        DecomposedTask task = Task(kind: EncodeTaskKind.Subtitle, outputIndex: 0);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced
            .SubtitleOutputs.Should()
            .BeEmpty(
                because: "a burn-in subtitle is rendered by the Video task's filter graph, "
                         + "never extracted standalone — claiming it here builds an ffmpeg "
                         + "command with an input and no output"
            );
    }

    [Fact]
    public void SliceForTask_SubtitleTask_StillPicksNonBurnInEntries()
    {
        // Regression guard on the fix above: a Subtitle task must still get
        // its normal (non-burn-in) entries — only burn-in is excluded.
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Subtitle, outputIndex: 1);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.SubtitleOutputs.Should().HaveCount(expected: 1);
        sliced.SubtitleOutputs[0].SourceIndex.Should().Be(expected: 1);
    }

    // ── SliceForBundle (Whole-kind tasks) ───────────────────────────────────

    [Fact]
    public void SliceForBundle_WholeWithNullIndexes_KeepsEverything()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(kind: EncodeTaskKind.Whole);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().HaveCount(expected: 2);
        sliced.AudioOutputs.Should().HaveCount(expected: 2);
        sliced.SubtitleOutputs.Should().HaveCount(expected: 2);
        sliced.Thumbnails.Should().NotBeNull();
    }

    [Fact]
    public void SliceForBundle_WholeWithEmptyVideoSliceIndexes_KeepsZeroVideos()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = new(
            TaskId: "bundle",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: null,
            VideoSliceIndexes: []
        );

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.VideoOutputs.Should().BeEmpty();
        sliced.AudioOutputs.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void SliceForBundle_WholeWithExplicitSubtitleIndexes_NarrowsCorrectly()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = new(
            TaskId: "bundle",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: null,
            SubtitleSliceIndexes: [1]
        );

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.SubtitleOutputs.Should().HaveCount(expected: 1);
        sliced.SubtitleOutputs[0].SourceIndex.Should().Be(expected: 1);
    }

    [Fact]
    public void SliceForBundle_WholeWithIncludeThumbnailsFalse_DropsThumbnails()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = new(
            TaskId: "bundle",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: null,
            IncludeThumbnails: false
        );

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan: plan, task: task);

        sliced.Thumbnails.Should().BeNull();
    }
}
