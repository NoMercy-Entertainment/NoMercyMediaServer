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
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class AnalyzeStageTests
{
    private readonly Mock<IMediaAnalyzer> _analyzer = new();
    private readonly Mock<IStorage> _storage = new();
    private readonly AnalyzeStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public AnalyzeStageTests()
    {
        _stage = new(_analyzer.Object, _storage.Object, NullLogger<AnalyzeStage>.Instance);
    }

    private static MediaInfo BuildMediaInfo() =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
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
                    6000
                ),
            ],
            [
                new(
                    1,
                    "aac",
                    2,
                    48000,
                    192,
                    "en",
                    true,
                    false
                ),
            ],
            [],
            []
        );

    // ------------------------------------------------------------------
    // File exists → success
    // ------------------------------------------------------------------

    [Fact]
    public async Task FileExists_AnalysisSucceeds_ReturnsMediaInfo()
    {
        MediaInfo expected = BuildMediaInfo();
        _storage.Setup(s => s.Exists("/movies/test.mkv")).Returns(true);
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    "/movies/test.mkv",
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        StageResult result = await _stage.ExecuteAsync("/movies/test.mkv", _context, default);

        result.Should().BeOfType<StageSuccess<MediaInfo>>();
        StageSuccess<MediaInfo> success = (StageSuccess<MediaInfo>)result;
        success.Value.Should().Be(expected);
        success.Value.VideoStreams.Should().HaveCount(1);
        success.Value.AudioStreams.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------
    // File missing → InputNotFound failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task FileMissing_ReturnsInputNotFoundFailure()
    {
        _storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        StageResult result = await _stage.ExecuteAsync("/missing/file.mkv", _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.InputNotFound);
        failure.Error.StageName.Should().Be("Analyze");
        failure.Error.Recoverable.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Analyzer throws → InputCorrupt failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnalyzerThrows_ReturnsInputCorruptFailure()
    {
        _storage.Setup(s => s.Exists("/corrupt.mkv")).Returns(true);
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync("/corrupt.mkv", It.IsAny<IStorage>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("ffprobe failed: invalid data"));

        StageResult result = await _stage.ExecuteAsync("/corrupt.mkv", _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.InputCorrupt);
        failure.Error.StageName.Should().Be("Analyze");
        failure.Error.Message.Should().Contain("ffprobe failed");
    }

    // ------------------------------------------------------------------
    // Cancellation propagates
    // ------------------------------------------------------------------

    [Fact]
    public async Task Cancellation_Propagates()
    {
        _storage.Setup(s => s.Exists("/movies/test.mkv")).Returns(true);
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    "/movies/test.mkv",
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());

        CancellationToken ct = new(true);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _stage.ExecuteAsync("/movies/test.mkv", _context, ct)
        );
    }
}
