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

using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// The variable-frame-rate decision <see cref="AnalyzeStage"/> emits rides the
/// <see cref="DecisionLog"/> the dashboard reads over the API. On a
/// comma-decimal server locale a bare ":F3" turns "29.970" into "29,970" in
/// that Message — this pins InvariantCulture on the formatter.
/// </summary>
public class AnalyzeStageCultureTests
{
    private readonly Mock<IMediaAnalyzer> _analyzer = new();
    private readonly Mock<IStorage> _storage = new();
    private readonly AnalyzeStage _stage;
    private readonly ScopedDecisionLog _log = new();
    private readonly EncodingContext _context;

    public AnalyzeStageCultureTests()
    {
        _stage = new(_analyzer.Object, _storage.Object, NullLogger<AnalyzeStage>.Instance);
        _context = new("test-correlation", Decisions: _log);
        _storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public async Task VfrDecisionMessage_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            _analyzer
                .Setup(a =>
                    a.AnalyzeAsync(
                        It.IsAny<string>(),
                        It.IsAny<IStorage>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(BuildMediaInfo(realFps: 30.0, avgFps: 24.0));

            await _stage.ExecuteAsync("/movies/x.mkv", _context, default);

            string message = _log.Snapshot().Single(d => d.Key == "analyze.vfr_detected").Message;
            message.Should().Contain("30.000").And.Contain("24.000");
            message.Should().NotContain("30,000").And.NotContain("24,000");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    private static MediaInfo BuildMediaInfo(double? realFps = null, double? avgFps = null) =>
        new(
            FilePath: "/movies/x.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
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
            Chapters: [],
            Attachments: [],
            DolbyVision: null
        );
}
