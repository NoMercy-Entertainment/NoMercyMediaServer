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
                .ReturnsAsync(BuildMediaInfo(30.0, 24.0));

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
            "/movies/x.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            8000,
            7_200_000_000,
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    6000,
                    avgFps,
                    realFps
                ),
            ],
            [],
            [],
            [],
            [],
            null
        );
}
