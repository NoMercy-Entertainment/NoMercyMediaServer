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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Covers <see cref="AnalyzeStage"/>'s source-quirk decision emission —
/// previously untested. The dashboard renders these decisions as chips:
/// "Dolby Vision detected", "Variable frame rate", "Fonts will be extracted",
/// "N chapters detected". Wrong-or-missing chips silently strip user-visible
/// signal from the encode workflow, so the chip-emission shape is a contract
/// worth pinning.
///
/// Each category below pins one source quirk:
///
/// • Dolby Vision — DolbyVisionInfo present → analyze.dv_present chip with
///   profile + level in the data payload.
/// • Variable frame rate — per-stream VFR detection emits one chip per VFR
///   stream; CFR streams produce no chip.
/// • Font attachments — analyze.attached_fonts fires when any attachment's
///   MIME contains "font", "truetype", or "opentype" (case-insensitive).
/// • Chapters — analyze.chapter_count fires when MediaInfo.Chapters is
///   non-empty; absent for chapter-less sources.
/// • All-quiet source — none of the chips fire when none of the conditions
///   apply (proves the absence path doesn't leak).
/// </summary>
public class AnalyzeStageQuirkTests
{
    private readonly Mock<IMediaAnalyzer> _analyzer = new();
    private readonly Mock<IStorage> _storage = new();
    private readonly AnalyzeStage _stage;

    public AnalyzeStageQuirkTests()
    {
        _stage = new(_analyzer.Object, _storage.Object, NullLogger<AnalyzeStage>.Instance);
    }

    private async Task<IReadOnlyList<DecisionLog>> ExecuteAndCollect(MediaInfo info)
    {
        _storage.Setup(s => s.Exists("/in.mkv")).Returns(true);
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync("/in.mkv", It.IsAny<IStorage>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(info);

        ScopedDecisionLog log = new();
        EncodingContext context = EncodingContext.Create() with { Decisions = log };

        await _stage.ExecuteAsync("/in.mkv", context, CancellationToken.None);

        return log.Snapshot();
    }

    // ── Dolby Vision ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Dolby_Vision_present_emits_dv_present_decision()
    {
        DolbyVisionInfo dv = new(
            Profile: 8,
            Level: 9,
            HasRpu: true,
            HasEl: false,
            BlCompat: DvBlCompatibility.Hdr10
        );
        MediaInfo info = BuildMedia(dolbyVision: dv);

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().ContainSingle(e => e.Key == "analyze.dv_present");
    }

    [Fact]
    public async Task Dolby_Vision_absent_does_not_emit_dv_chip()
    {
        MediaInfo info = BuildMedia(dolbyVision: null);

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().NotContain(e => e.Key == "analyze.dv_present");
    }

    // ── Variable frame rate ──────────────────────────────────────────────────

    [Fact]
    public async Task VFR_stream_emits_vfr_detected_decision()
    {
        // Real fps 30 vs average 24.5 = 18% spread → above the 1% threshold.
        VideoStreamInfo vfrStream = BuildVideoStream(realFps: 30.0, avgFps: 24.5);
        MediaInfo info = BuildMedia(videoStreams: [vfrStream]);

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().ContainSingle(e => e.Key == "analyze.vfr_detected");
    }

    [Fact]
    public async Task CFR_stream_does_not_emit_vfr_decision()
    {
        VideoStreamInfo cfrStream = BuildVideoStream(realFps: 24.0, avgFps: 24.0);
        MediaInfo info = BuildMedia(videoStreams: [cfrStream]);

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().NotContain(e => e.Key == "analyze.vfr_detected");
    }

    [Fact]
    public async Task Multiple_VFR_streams_each_emit_separate_decision()
    {
        VideoStreamInfo vfrA = BuildVideoStream(realFps: 30.0, avgFps: 24.5, index: 0);
        VideoStreamInfo vfrB = BuildVideoStream(realFps: 60.0, avgFps: 30.0, index: 1);
        VideoStreamInfo cfr = BuildVideoStream(realFps: 24.0, avgFps: 24.0, index: 2);
        MediaInfo info = BuildMedia(videoStreams: [vfrA, vfrB, cfr]);

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Count(e => e.Key == "analyze.vfr_detected").Should().Be(2);
    }

    [Fact]
    public async Task VFR_with_null_frame_rates_does_not_emit_decision()
    {
        // VideoStreamInfo.IsVariableFrameRate requires both fps fields populated
        // — when one is null, classification is "unknown" and no chip fires.
        VideoStreamInfo missingFps = BuildVideoStream(realFps: null, avgFps: 24.5);
        MediaInfo info = BuildMedia(videoStreams: [missingFps]);

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().NotContain(e => e.Key == "analyze.vfr_detected");
    }

    // ── Font attachments ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("application/x-font-truetype")]
    [InlineData("font/ttf")]
    [InlineData("application/x-font-opentype")]
    [InlineData("application/vnd.ms-opentype")]
    public async Task Font_attachment_with_any_font_mime_emits_attached_fonts(string mime)
    {
        MediaInfo info = BuildMedia(
            attachments: [new(Index: 0, Codec: "ttf", Filename: "Roboto.ttf", MimeType: mime)]
        );

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().ContainSingle(e => e.Key == "analyze.attached_fonts");
    }

    [Fact]
    public async Task Font_mime_match_is_case_insensitive()
    {
        MediaInfo info = BuildMedia(
            attachments:
            [
                new(
                    Index: 0,
                    Codec: "ttf",
                    Filename: "F.ttf",
                    MimeType: "APPLICATION/X-FONT-TRUETYPE"
                ),
            ]
        );

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().ContainSingle(e => e.Key == "analyze.attached_fonts");
    }

    [Fact]
    public async Task Non_font_attachment_does_not_emit_attached_fonts()
    {
        // Cover-art / poster image attachments should NOT trigger the font
        // chip — wrong chip mislabels the encode workflow.
        MediaInfo info = BuildMedia(
            attachments:
            [
                new(Index: 0, Codec: "png", Filename: "poster.png", MimeType: "image/png"),
            ]
        );

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().NotContain(e => e.Key == "analyze.attached_fonts");
    }

    [Fact]
    public async Task Attachment_with_null_mime_does_not_emit_chip()
    {
        // Malformed attachment with missing MimeType — must NOT crash and must
        // NOT emit the chip.
        MediaInfo info = BuildMedia(
            attachments: [new(Index: 0, Codec: "?", Filename: null, MimeType: null)]
        );

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().NotContain(e => e.Key == "analyze.attached_fonts");
    }

    [Fact]
    public async Task Multiple_font_attachments_emit_single_combined_chip()
    {
        // The font chip aggregates — one chip with a count, not N chips.
        MediaInfo info = BuildMedia(
            attachments:
            [
                new(0, "ttf", "F1.ttf", "application/x-font-truetype"),
                new(1, "ttf", "F2.ttf", "font/ttf"),
                new(2, "otf", "F3.otf", "application/x-font-opentype"),
            ]
        );

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Count(e => e.Key == "analyze.attached_fonts").Should().Be(1);
    }

    // ── Chapters ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Chapters_present_emits_chapter_count_decision()
    {
        MediaInfo info = BuildMedia(
            chapters:
            [
                new ChapterInfo(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Intro"),
                new ChapterInfo(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(45), "Act 1"),
            ]
        );

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().ContainSingle(e => e.Key == "analyze.chapter_count");
    }

    [Fact]
    public async Task No_chapters_does_not_emit_chapter_chip()
    {
        MediaInfo info = BuildMedia(chapters: []);

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().NotContain(e => e.Key == "analyze.chapter_count");
    }

    // ── Quiet source ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Vanilla_source_emits_no_quirk_chips()
    {
        MediaInfo info = BuildMedia(); // all defaults

        IReadOnlyList<DecisionLog> log = await ExecuteAndCollect(info);

        log.Should().NotContain(e => e.Key.StartsWith("analyze.dv_"));
        log.Should().NotContain(e => e.Key == "analyze.vfr_detected");
        log.Should().NotContain(e => e.Key == "analyze.attached_fonts");
        log.Should().NotContain(e => e.Key == "analyze.chapter_count");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MediaInfo BuildMedia(
        IReadOnlyList<VideoStreamInfo>? videoStreams = null,
        IReadOnlyList<AttachmentInfo>? attachments = null,
        IReadOnlyList<ChapterInfo>? chapters = null,
        DolbyVisionInfo? dolbyVision = null
    ) =>
        new(
            FilePath: "/in.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(1),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 0,
            VideoStreams: videoStreams ?? [BuildVideoStream()],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: chapters ?? [],
            Attachments: attachments ?? [],
            DolbyVision: dolbyVision
        );

    private static VideoStreamInfo BuildVideoStream(
        double? realFps = 24.0,
        double? avgFps = 24.0,
        int index = 0
    ) =>
        new(
            Index: index,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: avgFps ?? 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 5000,
            AverageFrameRate: avgFps,
            RealFrameRate: realFps
        );
}
