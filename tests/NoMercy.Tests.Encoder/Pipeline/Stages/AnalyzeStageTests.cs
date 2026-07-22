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
        _stage = new(analyzer: _analyzer.Object, storage: _storage.Object, logger: NullLogger<AnalyzeStage>.Instance);
    }

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
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
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "en",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

    // ------------------------------------------------------------------
    // File exists → success
    // ------------------------------------------------------------------

    [Fact]
    public async Task FileExists_AnalysisSucceeds_ReturnsMediaInfo()
    {
        MediaInfo expected = BuildMediaInfo();
        _storage.Setup(expression: s => s.Exists("/movies/test.mkv")).Returns(value: true);
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    "/movies/test.mkv",
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: expected);

        StageResult result = await _stage.ExecuteAsync(inputPath: "/movies/test.mkv", context: _context, ct: default);

        result.Should().BeOfType<StageSuccess<MediaInfo>>();
        StageSuccess<MediaInfo> success = (StageSuccess<MediaInfo>)result;
        success.Value.Should().Be(expected: expected);
        success.Value.VideoStreams.Should().HaveCount(expected: 1);
        success.Value.AudioStreams.Should().HaveCount(expected: 1);
    }

    // ------------------------------------------------------------------
    // File missing → InputNotFound failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task FileMissing_ReturnsInputNotFoundFailure()
    {
        _storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: false);

        StageResult result = await _stage.ExecuteAsync(inputPath: "/missing/file.mkv", context: _context, ct: default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(expected: EncodingErrorKind.InputNotFound);
        failure.Error.StageName.Should().Be(expected: "Analyze");
        failure.Error.Recoverable.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Analyzer throws → InputCorrupt failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnalyzerThrows_ReturnsInputCorruptFailure()
    {
        _storage.Setup(expression: s => s.Exists("/corrupt.mkv")).Returns(value: true);
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync("/corrupt.mkv", It.IsAny<IStorage>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "ffprobe failed: invalid data"));

        StageResult result = await _stage.ExecuteAsync(inputPath: "/corrupt.mkv", context: _context, ct: default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(expected: EncodingErrorKind.InputCorrupt);
        failure.Error.StageName.Should().Be(expected: "Analyze");
        failure.Error.Message.Should().Contain(expected: "ffprobe failed");
    }

    // ------------------------------------------------------------------
    // Cancellation propagates
    // ------------------------------------------------------------------

    [Fact]
    public async Task Cancellation_Propagates()
    {
        _storage.Setup(expression: s => s.Exists("/movies/test.mkv")).Returns(value: true);
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    "/movies/test.mkv",
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new OperationCanceledException());

        CancellationToken ct = new(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(testCode: () =>
            _stage.ExecuteAsync(inputPath: "/movies/test.mkv", context: _context, ct: ct)
        );
    }
}
