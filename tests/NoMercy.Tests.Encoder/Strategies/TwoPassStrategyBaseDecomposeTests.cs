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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Strategies;

/// <summary>
/// Decomposition contract for the two-pass strategy family. Pass-2 quality
/// depends on Pass-1 stats — the coordinator MUST run every Pass1 task to
/// completion and propagate the stats file before enqueuing Pass2. If the
/// task graph drops a Pass1 or shuffles indices, the encoder ends up either
/// running Pass2 without stats (silent quality regression) or doubling work
/// for some variants.
///
/// Each category below pins a structural guarantee that <c>EncodeTaskJob</c>'s
/// coordinator depends on:
///
/// • One Pass1 + one Pass2 per video variant — never zero, never duplicated.
/// • Pass1 tasks emit BEFORE Pass2 tasks — task-list scan order is what the
///   coordinator schedules on.
/// • TaskId pattern `{groupTag}-pass1-{i}` / `{groupTag}-pass2-{i}` — what
///   the dispatcher de-dupes on.
/// • Cost banding by width — same shape as single-pass strategies (no HDR
///   bump in two-pass video cost).
/// • Audio / subtitle / thumbnail / chapter tasks unchanged by the two-pass
///   shape — they run concurrent with Pass2.
/// • Thumbnails task is added when Thumbnails plan present (unconditional —
///   unlike chapter thumbs which are opt-in).
/// • Empty plan falls back to a single Whole task per the IEncodingStrategy
///   contract.
/// </summary>
public class TwoPassStrategyBaseDecomposeTests
{
    private const string GroupTag = "two-pass-g";

    private static HlsTwoPassStrategy Build() =>
        new(
            Mock.Of<IEncoder>(),
            Mock.Of<ICheckpointStore>(),
            NullLogger<HlsTwoPassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

    // ── Pass1 + Pass2 per variant ────────────────────────────────────────────

    [Fact]
    public void Decompose_one_video_variant_emits_one_pass1_and_one_pass2()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = Build().Decompose(plan, GroupTag);

        tasks.Should().HaveCount(2);
        tasks.Count(t => t.Kind == EncodeTaskKind.Pass1).Should().Be(1);
        tasks.Count(t => t.Kind == EncodeTaskKind.Pass2).Should().Be(1);
    }

    [Fact]
    public void Decompose_three_variants_emits_three_pass1_and_three_pass2()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                Video(width: 1920, height: 1080, encoderName: "libx264"),
                Video(width: 1280, height: 720, encoderName: "libx264"),
                Video(width: 854, height: 480, encoderName: "libx264"),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = Build().Decompose(plan, GroupTag);

        tasks.Count(t => t.Kind == EncodeTaskKind.Pass1).Should().Be(3);
        tasks.Count(t => t.Kind == EncodeTaskKind.Pass2).Should().Be(3);
    }

    [Fact]
    public void Decompose_all_pass1_tasks_emit_before_any_pass2_task()
    {
        // Coordinator depends on iteration order — pass1 tasks first so they
        // can be enqueued together before any pass2 child appears.
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080), Video(width: 1280, height: 720)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = Build().Decompose(plan, GroupTag);

        int lastPass1Index = -1;
        int firstPass2Index = int.MaxValue;
        for (int i = 0; i < tasks.Length; i++)
        {
            if (tasks[i].Kind == EncodeTaskKind.Pass1)
                lastPass1Index = Math.Max(lastPass1Index, i);
            if (tasks[i].Kind == EncodeTaskKind.Pass2 && i < firstPass2Index)
                firstPass2Index = i;
        }

        lastPass1Index.Should().BeLessThan(firstPass2Index);
    }

    [Fact]
    public void Decompose_pass1_and_pass2_task_ids_use_separate_index_namespaces()
    {
        // Each pass numbers from 0 independently — coordinator pairs by index.
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080), Video(width: 1280, height: 720)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = Build().Decompose(plan, GroupTag);

        DecomposedTask[] pass1 = tasks.Where(t => t.Kind == EncodeTaskKind.Pass1).ToArray();
        DecomposedTask[] pass2 = tasks.Where(t => t.Kind == EncodeTaskKind.Pass2).ToArray();

        pass1.Select(t => t.TaskId).Should().Equal($"{GroupTag}-pass1-0", $"{GroupTag}-pass1-1");
        pass2.Select(t => t.TaskId).Should().Equal($"{GroupTag}-pass2-0", $"{GroupTag}-pass2-1");
    }

    [Fact]
    public void Decompose_pass2_task_starts_with_null_stats_file_path()
    {
        // Decompose can't know the stats path yet — that's set by the
        // coordinator after Pass1 completes. Default must be null.
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask pass2 = Build()
            .Decompose(plan, GroupTag)
            .Single(t => t.Kind == EncodeTaskKind.Pass2);

        pass2.StatsFilePath.Should().BeNull();
    }

    // ── Label includes pass + resolution ────────────────────────────────────

    [Fact]
    public void Decompose_pass1_label_includes_resolution_and_encoder()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080, encoderName: "libx264")],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask pass1 = Build()
            .Decompose(plan, GroupTag)
            .Single(t => t.Kind == EncodeTaskKind.Pass1);

        pass1.Label.Should().StartWith("pass1 ").And.Contain("1920p").And.Contain("libx264");
    }

    [Fact]
    public void Decompose_pass2_label_starts_with_pass2_token()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1280, height: 720, encoderName: "libx264")],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask pass2 = Build()
            .Decompose(plan, GroupTag)
            .Single(t => t.Kind == EncodeTaskKind.Pass2);

        pass2.Label.Should().StartWith("pass2 ").And.Contain("1280p");
    }

    // ── Cost banding ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3840, 8)]
    [InlineData(1920, 4)]
    [InlineData(1280, 2)]
    [InlineData(854, 1)]
    public void Decompose_video_cost_matches_width_for_both_passes(int width, int expectedCost)
    {
        // Pass1 and Pass2 do roughly equivalent CPU work per variant — cost
        // bands must match so the dispatcher allocates consistent slots.
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: width, height: Math.Max(180, width * 9 / 16))],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = Build().Decompose(plan, GroupTag);

        tasks
            .Single(t => t.Kind == EncodeTaskKind.Pass1)
            .EstimatedCostUnits.Should()
            .Be(expectedCost);
        tasks
            .Single(t => t.Kind == EncodeTaskKind.Pass2)
            .EstimatedCostUnits.Should()
            .Be(expectedCost);
    }

    // ── Audio / subtitle / thumbnails / chapter expansion ────────────────────

    [Fact]
    public void Decompose_audio_outputs_emit_one_task_each_with_und_label_fallback()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "[a0]"
                ),
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: null,
                    MapLabel: "[a1]"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] audio = Build()
            .Decompose(plan, GroupTag)
            .Where(t => t.Kind == EncodeTaskKind.Audio)
            .ToArray();

        audio.Should().HaveCount(2);
        audio[0].Label.Should().Be("eng aac");
        audio[1].Label.Should().Be("und aac"); // null language → und
    }

    [Fact]
    public void Decompose_subtitle_outputs_emit_one_task_each_with_und_label_fallback()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [Subtitle("eng", 0), Subtitle(null, 1)],
            Thumbnails: null
        );

        DecomposedTask[] subs = Build()
            .Decompose(plan, GroupTag)
            .Where(t => t.Kind == EncodeTaskKind.Subtitle)
            .ToArray();

        subs.Should().HaveCount(2);
        subs[0].Label.Should().Be("sub eng");
        subs[1].Label.Should().Be("sub und");
    }

    [Fact]
    public void Decompose_thumbnails_plan_emits_single_thumbnails_task()
    {
        // Unlike single-pass, two-pass emits a single named "thumbnails" task
        // with cost=1 — the actual frame-rate logic lives in BuildStage.
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: new ThumbnailOutputPlan(160, 90, 10)
        );

        DecomposedTask[] thumbs = Build()
            .Decompose(plan, GroupTag)
            .Where(t => t.Kind == EncodeTaskKind.Thumbnails)
            .ToArray();

        thumbs.Should().ContainSingle();
        thumbs[0].TaskId.Should().Be($"{GroupTag}-thumbs");
        thumbs[0].Label.Should().Be("thumbnails");
        thumbs[0].EstimatedCostUnits.Should().Be(1);
    }

    [Fact]
    public void Decompose_thumbnails_null_omits_thumbnail_task()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        Build()
            .Decompose(plan, GroupTag)
            .Should()
            .NotContain(t => t.Kind == EncodeTaskKind.Thumbnails);
    }

    [Fact]
    public void Decompose_chapter_thumbs_opt_in_emits_one_task_per_chapter()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Intro"),
                new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(45), "Act 1"),
            ],
            GenerateChapterThumbs: true
        );

        DecomposedTask[] chapters = Build()
            .Decompose(plan, GroupTag)
            .Where(t => t.Kind == EncodeTaskKind.Chapters)
            .ToArray();

        chapters.Should().HaveCount(2);
        chapters[0].TaskId.Should().Be($"{GroupTag}-chapter-0");
        chapters[0].Label.Should().Contain("1/2").And.Contain("@ 0s");
        chapters[1].Label.Should().Contain("2/2").And.Contain("@ 600s");
    }

    [Fact]
    public void Decompose_chapter_thumbs_opt_out_emits_no_chapter_tasks()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: [new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Intro")],
            GenerateChapterThumbs: false
        );

        Build()
            .Decompose(plan, GroupTag)
            .Should()
            .NotContain(t => t.Kind == EncodeTaskKind.Chapters);
    }

    [Fact]
    public void Decompose_chapter_thumbs_opt_in_but_no_chapters_emits_no_chapter_tasks()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: [],
            GenerateChapterThumbs: true
        );

        Build()
            .Decompose(plan, GroupTag)
            .Should()
            .NotContain(t => t.Kind == EncodeTaskKind.Chapters);
    }

    // ── Empty plan fallback ──────────────────────────────────────────────────

    [Fact]
    public void Decompose_empty_plan_returns_single_whole_task()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = Build().Decompose(plan, GroupTag);

        tasks.Should().ContainSingle();
        tasks[0].Kind.Should().Be(EncodeTaskKind.Whole);
    }

    // ── Full graph ordering ──────────────────────────────────────────────────

    [Fact]
    public void Decompose_full_plan_emits_pass1_then_pass2_then_audio_then_subs_then_thumbs_then_chapters()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(), Video(width: 1280, height: 720)],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "[a0]"
                ),
            ],
            SubtitleOutputs: [Subtitle("eng", 0)],
            Thumbnails: new ThumbnailOutputPlan(160, 90, 10),
            Chapters: [new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Intro")],
            GenerateChapterThumbs: true
        );

        DecomposedTask[] tasks = Build().Decompose(plan, GroupTag);

        // Block sequence: 2× Pass1, 2× Pass2, 1× Audio, 1× Subtitle, 1× Thumbnails, 1× Chapters = 8 total
        tasks.Should().HaveCount(8);
        tasks
            .Select(t => t.Kind)
            .Should()
            .Equal(
                EncodeTaskKind.Pass1,
                EncodeTaskKind.Pass1,
                EncodeTaskKind.Pass2,
                EncodeTaskKind.Pass2,
                EncodeTaskKind.Audio,
                EncodeTaskKind.Subtitle,
                EncodeTaskKind.Thumbnails,
                EncodeTaskKind.Chapters
            );
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static VideoOutputPlan Video(
        int width = 1920,
        int height = 1080,
        string encoderName = "libx264"
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoderName,
            Crf: 23,
            BitrateKbps: 5000,
            Preset: "medium",
            Profile: "high",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: []
        );

    private static SubtitleOutputPlan Subtitle(string? language, int sourceIndex) =>
        new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Transcode,
            Language: language,
            SourceIndex: sourceIndex,
            MapLabel: $"[s{sourceIndex}]"
        );
}
