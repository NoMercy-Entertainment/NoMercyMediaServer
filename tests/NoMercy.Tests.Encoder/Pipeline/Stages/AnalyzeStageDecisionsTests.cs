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
        _stage = new AnalyzeStage(
            _analyzer.Object,
            _storage.Object,
            NullLogger<AnalyzeStage>.Instance
        );
        _context = new EncodingContext("test-correlation", Decisions: _log);
        _storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);
    }

    [Fact]
    public async Task DolbyVision_present_emits_dv_present_decision()
    {
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                BuildMediaInfo(
                    dolbyVision: new DolbyVisionInfo(7, 6, true, false, DvBlCompatibility.Hdr10)
                )
            );

        await _stage.ExecuteAsync("/movies/x.mkv", _context, default);

        _log.Snapshot().Should().Contain(d => d.Key == "analyze.dv_present");
    }

    [Fact]
    public async Task No_DolbyVision_does_not_emit_dv_present_decision()
    {
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildMediaInfo());

        await _stage.ExecuteAsync("/movies/x.mkv", _context, default);

        _log.Snapshot().Should().NotContain(d => d.Key == "analyze.dv_present");
    }

    [Fact]
    public async Task Variable_frame_rate_emits_vfr_detected_decision()
    {
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

        _log.Snapshot().Should().Contain(d => d.Key == "analyze.vfr_detected");
    }

    [Fact]
    public async Task Constant_frame_rate_does_not_emit_vfr_detected_decision()
    {
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(BuildMediaInfo(realFps: 23.976, avgFps: 23.976));

        await _stage.ExecuteAsync("/movies/x.mkv", _context, default);

        _log.Snapshot().Should().NotContain(d => d.Key == "analyze.vfr_detected");
    }

    [Fact]
    public async Task Font_attachments_emit_attached_fonts_decision()
    {
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                BuildMediaInfo(
                    attachments:
                    [
                        new AttachmentInfo(
                            99,
                            "ttf",
                            "OpenSans-Regular.ttf",
                            "application/x-truetype-font"
                        ),
                        new AttachmentInfo(100, "ttf", "OpenSans-Bold.ttf", "font/ttf"),
                    ]
                )
            );

        await _stage.ExecuteAsync("/movies/x.mkv", _context, default);

        _log.Snapshot().Should().Contain(d => d.Key == "analyze.attached_fonts");
    }

    [Fact]
    public async Task Chapters_emit_chapter_count_decision()
    {
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                BuildMediaInfo(
                    chapters:
                    [
                        new ChapterInfo(TimeSpan.Zero, TimeSpan.FromMinutes(5), "Open"),
                        new ChapterInfo(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), "Act 1"),
                    ]
                )
            );

        await _stage.ExecuteAsync("/movies/x.mkv", _context, default);

        _log.Snapshot().Should().Contain(d => d.Key == "analyze.chapter_count");
    }

    [Fact]
    public async Task No_decisions_emitted_when_context_has_no_sink()
    {
        EncodingContext bare = new("no-sink");
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    It.IsAny<string>(),
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                BuildMediaInfo(
                    dolbyVision: new DolbyVisionInfo(7, 6, true, false, DvBlCompatibility.Hdr10)
                )
            );

        // Should not throw — no-op sink swallows.
        await _stage.ExecuteAsync("/movies/x.mkv", bare, default);

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
            Chapters: chapters ?? [],
            Attachments: attachments ?? [],
            DolbyVision: dolbyVision
        );
}
