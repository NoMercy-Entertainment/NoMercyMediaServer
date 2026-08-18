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
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies.Dash;

/// <summary>
/// Decomposition contract for DASH single-pass. DASH was previously covered
/// only via DashMultiVariantTwoPassTests, which exercises the two-pass shape.
/// The single-pass variant is what every "encode to DASH" preset hits, and
/// its task graph determines dispatcher load shape: one Video task per
/// rendition, one Audio per group, one Subtitle per track, one Chapter task
/// per chapter when chapter-thumbs are opted in. Each category below pins a
/// specific failure mode — wrong task counts, dropped cost banding, missing
/// chapter expansion — that would silently misroute work under load.
/// </summary>
public class DashSinglePassStrategyTests
{
    // ── Format + EncodeMode contract ─────────────────────────────────────────

    [Fact]
    public void Format_IsDash()
    {
        // Dispatcher routes on OutputFormat — wrong value sends DASH jobs through
        // the HLS pipeline.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        strategy.Format.Should().Be(OutputFormat.Dash);
    }

    [Fact]
    public void EncodeMode_IsSinglePass()
    {
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        strategy.EncodeMode.Should().Be(EncodeMode.SinglePass);
    }

    // ── EncodeAsync delegation ───────────────────────────────────────────────

    [Fact]
    public async Task EncodeAsync_delegates_to_injected_encoder()
    {
        // Strategy must not re-invent the wheel — the underlying IEncoder owns
        // the actual ffmpeg invocation. Verify pass-through arguments.
        Mock<IEncoder> encoder = new();
        EncodingResult expected = new(
            Success: true,
            OutputPath: "/out",
            Duration: TimeSpan.FromSeconds(1),
            Error: null,
            Metrics: new(1024, 2.0, 24.0, "libx264", null)
        );
        encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        DashSinglePassStrategy strategy = new(
            encoder.Object,
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        EncodingRequest request = new(
            InputPath: "/media/test.mkv",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "DASH 1080p",
                Container: Container.Dash,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Should().BeSameAs(expected);
        encoder.Verify(
            e =>
                e.EncodeAsync(
                    request,
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── EstimateVideoCost banding (private — tested via Decompose) ───────────

    [Theory]
    [InlineData(3840, 8)] // 4K → 8 units
    [InlineData(4096, 8)] // 4K DCI → 8 units (>= 3840)
    [InlineData(1920, 4)] // 1080p → 4 units
    [InlineData(2560, 4)] // 1440p → 4 units (>= 1920, <3840)
    [InlineData(1280, 2)] // 720p → 2 units
    [InlineData(1600, 2)] // 900p → 2 units (>= 1280, <1920)
    [InlineData(854, 1)] // 480p → 1 unit
    [InlineData(640, 1)] // 360p → 1 unit
    [InlineData(0, 1)] // degenerate → 1 unit (default branch)
    public void Decompose_video_cost_banding_matches_width(int width, int expectedCost)
    {
        // Cost units gate dispatcher concurrency — wrong banding = wrong bundle
        // sizing under load. Unlike HLS, DASH single-pass has no HDR-tonemap
        // bump, so banding is the only cost input.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [Video(width: width, height: Math.Max(180, width * 9 / 16))],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask video = strategy
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Video);

        video.EstimatedCostUnits.Should().Be(expectedCost);
    }

    [Fact]
    public void Decompose_hdr_to_sdr_does_not_add_cost_bump()
    {
        // Documented contrast with HLS single-pass: HLS adds +1 for tonemap,
        // DASH does not. If anyone unifies the EstimateVideoCost helpers, this
        // test pins the current cost shape so the change is intentional.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [Video(width: 1920, height: 1080, convertHdrToSdr: true)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask video = strategy
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Video);

        video.EstimatedCostUnits.Should().Be(4); // 1080p baseline, no HDR bump
    }

    // ── Task IDs + labels + grouping ─────────────────────────────────────────

    [Fact]
    public void Decompose_emits_one_video_task_per_rendition_with_indexed_ids()
    {
        // TaskId pattern `{groupTag}-video-{i}` is what the dispatcher de-duplicates on.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
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

        DecomposedTask[] videoTasks =
        [
            .. strategy.Decompose(plan, "gtag").Where(t => t.Kind == EncodeTaskKind.Video),
        ];

        videoTasks.Should().HaveCount(3);
        videoTasks[0].TaskId.Should().Be("gtag-video-0");
        videoTasks[1].TaskId.Should().Be("gtag-video-1");
        videoTasks[2].TaskId.Should().Be("gtag-video-2");
        videoTasks[0].GroupTag.Should().Be("gtag");
        videoTasks[0].Label.Should().Contain("1920p").And.Contain("libx264");
    }

    [Fact]
    public void Decompose_audio_task_label_carries_language_and_encoder()
    {
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "fre",
                    MapLabel: "[a0]"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask audio = strategy
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Audio);

        audio.TaskId.Should().Be("g-audio-0");
        audio.Label.Should().Be("fre aac");
        audio.EstimatedCostUnits.Should().Be(1); // audio is always CPU-1
    }

    [Fact]
    public void Decompose_audio_task_label_uses_und_when_language_missing()
    {
        // No language → "und" (ISO 639-2 undetermined) — must not crash on null.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: null,
                    MapLabel: "[a0]"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask audio = strategy
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Audio);

        audio.Label.Should().StartWith("und ");
    }

    [Fact]
    public void Decompose_subtitle_task_label_uses_und_when_language_missing()
    {
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Transcode,
                    Language: null,
                    SourceIndex: 0,
                    MapLabel: "[s0]"
                ),
            ],
            Thumbnails: null
        );

        DecomposedTask sub = strategy
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Subtitle);

        sub.TaskId.Should().Be("g-sub-0");
        sub.Label.Should().Be("sub und");
    }

    [Fact]
    public void Decompose_multiple_subtitles_each_get_indexed_task()
    {
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [Subtitle("eng", 0), Subtitle("fre", 1), Subtitle("ger", 2)],
            Thumbnails: null
        );

        DecomposedTask[] subs =
        [
            .. strategy.Decompose(plan, "g").Where(t => t.Kind == EncodeTaskKind.Subtitle),
        ];

        subs.Should().HaveCount(3);
        subs.Select(s => s.TaskId).Should().Equal("g-sub-0", "g-sub-1", "g-sub-2");
        subs.Select(s => s.Label).Should().Equal("sub eng", "sub fre", "sub ger");
    }

    // ── Chapter thumbs branch ────────────────────────────────────────────────

    [Fact]
    public void Decompose_chapter_thumbs_opt_in_emits_one_task_per_chapter()
    {
        // Opt-in chapter thumbs expand to one Chapters task per chapter so
        // BuildStage can extract a still per timestamp in parallel.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters:
            [
                new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Intro"),
                new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(45), "Act 1"),
                new(TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(90), "Act 2"),
            ],
            GenerateChapterThumbs: true
        );

        DecomposedTask[] chapters =
        [
            .. strategy.Decompose(plan, "g").Where(t => t.Kind == EncodeTaskKind.Chapters),
        ];

        chapters.Should().HaveCount(3);
        chapters[0].TaskId.Should().Be("g-chapter-0");
        chapters[1].TaskId.Should().Be("g-chapter-1");
        chapters[2].TaskId.Should().Be("g-chapter-2");
        chapters[0].Label.Should().Contain("1/3").And.Contain("@ 0s");
        chapters[1].Label.Should().Contain("2/3").And.Contain("@ 600s");
    }

    [Fact]
    public void Decompose_chapter_thumbs_opt_out_skips_chapter_tasks()
    {
        // Opt-out (default false) — chapters present but no tasks emitted.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: [new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Intro")],
            GenerateChapterThumbs: false
        );

        DecomposedTask[] chapters =
        [
            .. strategy.Decompose(plan, "g").Where(t => t.Kind == EncodeTaskKind.Chapters),
        ];

        chapters.Should().BeEmpty();
    }

    [Fact]
    public void Decompose_chapter_thumbs_opt_in_but_empty_chapters_skips()
    {
        // Opt-in but Chapters is empty — guard predicate `is { Count: > 0 }`
        // must short-circuit cleanly without IndexOutOfRange.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: [],
            GenerateChapterThumbs: true
        );

        DecomposedTask[] chapters =
        [
            .. strategy.Decompose(plan, "g").Where(t => t.Kind == EncodeTaskKind.Chapters),
        ];

        chapters.Should().BeEmpty();
    }

    [Fact]
    public void Decompose_chapter_thumbs_opt_in_but_null_chapters_skips()
    {
        // Opt-in but Chapters is null — guard predicate must short-circuit.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: null,
            GenerateChapterThumbs: true
        );

        DecomposedTask[] chapters =
        [
            .. strategy.Decompose(plan, "g").Where(t => t.Kind == EncodeTaskKind.Chapters),
        ];

        chapters.Should().BeEmpty();
    }

    // ── Empty plan fallback ──────────────────────────────────────────────────

    [Fact]
    public void Decompose_no_outputs_returns_whole_task_fallback()
    {
        // Strategy contract: always at least one task — fallback to a single
        // "whole" task if no outputs declared.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, "g");

        tasks.Should().ContainSingle();
        tasks[0].Kind.Should().Be(EncodeTaskKind.Whole);
    }

    [Fact]
    public void Decompose_full_plan_returns_tasks_in_video_audio_sub_chapter_order()
    {
        // Order matters for dispatcher scheduling — video first (cost-heavy),
        // then audio, then subs, then chapters last.
        DashSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<DashSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [Video()],
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
            Thumbnails: null,
            Chapters: [new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Intro")],
            GenerateChapterThumbs: true
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, "g");

        tasks.Should().HaveCount(4);
        tasks
            .Select(t => t.Kind)
            .Should()
            .Equal(
                EncodeTaskKind.Video,
                EncodeTaskKind.Audio,
                EncodeTaskKind.Subtitle,
                EncodeTaskKind.Chapters
            );
    }

    // ── Builders ─────────────────────────────────────────────────────────────

    private static VideoOutputPlan Video(
        int width = 1280,
        int height = 720,
        string encoderName = "libx264",
        bool convertHdrToSdr = false
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoderName,
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: []
        )
        {
            ConvertHdrToSdr = convertHdrToSdr,
        };

    private static SubtitleOutputPlan Subtitle(string? language, int sourceIndex) =>
        new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Transcode,
            Language: language,
            SourceIndex: sourceIndex,
            MapLabel: $"[s{sourceIndex}]"
        );
}
