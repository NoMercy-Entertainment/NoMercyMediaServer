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

public class AnalyzeStageDecisionsTests
{
    private readonly Mock<IMediaAnalyzer> _analyzer = new();
    private readonly Mock<IStorage> _storage = new();
    private readonly AnalyzeStage _stage;
    private readonly ScopedDecisionLog _log = new();
    private readonly EncodingContext _context;

    public AnalyzeStageDecisionsTests()
    {
        _stage = new(
            analyzer: _analyzer.Object,
            storage: _storage.Object,
            logger: NullLogger<AnalyzeStage>.Instance
        );
        _context = new(CorrelationId: "test-correlation", Decisions: _log);
        _storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
    }

    [Fact]
    public async Task DolbyVision_present_emits_dv_present_decision()
    {
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: BuildMediaInfo(
                    dolbyVision: new(Profile: 7, Level: 6, HasRpu: true, HasEl: false, BlCompat: DvBlCompatibility.Hdr10)
                )
            );

        await _stage.ExecuteAsync(inputPath: "/movies/x.mkv", context: _context, ct: default);

        _log.Snapshot().Should().Contain(predicate: d => d.Key == "analyze.dv_present");
    }

    [Fact]
    public async Task No_DolbyVision_does_not_emit_dv_present_decision()
    {
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: BuildMediaInfo());

        await _stage.ExecuteAsync(inputPath: "/movies/x.mkv", context: _context, ct: default);

        _log.Snapshot().Should().NotContain(predicate: d => d.Key == "analyze.dv_present");
    }

    [Fact]
    public async Task Variable_frame_rate_emits_vfr_detected_decision()
    {
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: BuildMediaInfo(realFps: 30.0, avgFps: 24.0));

        await _stage.ExecuteAsync(inputPath: "/movies/x.mkv", context: _context, ct: default);

        _log.Snapshot().Should().Contain(predicate: d => d.Key == "analyze.vfr_detected");
    }

    [Fact]
    public async Task Constant_frame_rate_does_not_emit_vfr_detected_decision()
    {
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: BuildMediaInfo(realFps: 23.976, avgFps: 23.976));

        await _stage.ExecuteAsync(inputPath: "/movies/x.mkv", context: _context, ct: default);

        _log.Snapshot().Should().NotContain(predicate: d => d.Key == "analyze.vfr_detected");
    }

    [Fact]
    public async Task Font_attachments_emit_attached_fonts_decision()
    {
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: BuildMediaInfo(
                    attachments:
                    [
                        new(
                            Index: 99,
                            Codec: "ttf",
                            Filename: "OpenSans-Regular.ttf",
                            MimeType: "application/x-truetype-font"
                        ),
                        new(Index: 100, Codec: "ttf", Filename: "OpenSans-Bold.ttf", MimeType: "font/ttf"),
                    ]
                )
            );

        await _stage.ExecuteAsync(inputPath: "/movies/x.mkv", context: _context, ct: default);

        _log.Snapshot().Should().Contain(predicate: d => d.Key == "analyze.attached_fonts");
    }

    [Fact]
    public async Task Chapters_emit_chapter_count_decision()
    {
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: BuildMediaInfo(
                    chapters:
                    [
                        new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: "Open"),
                        new(Start: TimeSpan.FromMinutes(minutes: 5), End: TimeSpan.FromMinutes(minutes: 10), Title: "Act 1"),
                    ]
                )
            );

        await _stage.ExecuteAsync(inputPath: "/movies/x.mkv", context: _context, ct: default);

        _log.Snapshot().Should().Contain(predicate: d => d.Key == "analyze.chapter_count");
    }

    [Fact]
    public async Task No_decisions_emitted_when_context_has_no_sink()
    {
        EncodingContext bare = new(CorrelationId: "no-sink");
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: BuildMediaInfo(
                    dolbyVision: new(Profile: 7, Level: 6, HasRpu: true, HasEl: false, BlCompat: DvBlCompatibility.Hdr10)
                )
            );

        // Should not throw — no-op sink swallows.
        await _stage.ExecuteAsync(inputPath: "/movies/x.mkv", context: bare, ct: default);

        bare.DecisionsOrNoOp.Snapshot().Should().BeEmpty();
    }

    private static MediaInfo BuildMediaInfo(
        DolbyVisionInfo? dolbyVision = null,
        double? realFps = null,
        double? avgFps = null,
        IReadOnlyList<AttachmentInfo>? attachments = null,
        IReadOnlyList<ChapterInfo>? chapters = null
    ) =>
        new(
            FilePath: "/movies/x.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: 1920,
                    Height: 1080,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000,
                    AverageFrameRate: avgFps,
                    RealFrameRate: realFps
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: chapters ?? [],
            Attachments: attachments ?? [],
            DolbyVision: dolbyVision
        );
}
