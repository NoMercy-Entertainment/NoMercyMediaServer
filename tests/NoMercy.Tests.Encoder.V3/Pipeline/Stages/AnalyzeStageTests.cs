namespace NoMercy.Tests.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Infrastructure;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Pipeline.Stages;

public class AnalyzeStageTests
{
    private readonly Mock<IMediaAnalyzer> _analyzer = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly AnalyzeStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public AnalyzeStageTests()
    {
        _stage = new AnalyzeStage(
            _analyzer.Object,
            _fileSystem.Object,
            NullLogger<AnalyzeStage>.Instance
        );
    }

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new VideoStreamInfo(
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
                new AudioStreamInfo(
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
        _fileSystem.Setup(fs => fs.FileExists("/movies/test.mkv")).Returns(true);
        _analyzer
            .Setup(a => a.AnalyzeAsync("/movies/test.mkv", It.IsAny<CancellationToken>()))
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
        _fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

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
        _fileSystem.Setup(fs => fs.FileExists("/corrupt.mkv")).Returns(true);
        _analyzer
            .Setup(a => a.AnalyzeAsync("/corrupt.mkv", It.IsAny<CancellationToken>()))
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
        _fileSystem.Setup(fs => fs.FileExists("/movies/test.mkv")).Returns(true);
        _analyzer
            .Setup(a => a.AnalyzeAsync("/movies/test.mkv", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        CancellationToken ct = new CancellationToken(true);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _stage.ExecuteAsync("/movies/test.mkv", _context, ct)
        );
    }
}
