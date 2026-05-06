using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Bundle;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Verifies that FinalizeStage gates each generator call on the matching
/// HlsDerivatives flag. Generators behind IChapterWriter / IFontExtractor
/// interfaces are verified via Moq. Generators that live outside FinalizeStage
/// (sprite VTT via FFmpeg muxer, I-frame playlists, CC extraction) are
/// documented as no-ops with TODOs inside FinalizeStage; those flags are
/// tested only to confirm they don't throw.
/// </summary>
public class FinalizeStageHlsDerivativesTests
{
    // ── Test infrastructure ───────────────────────────────────────────────────

    private static ExecutionResult SuccessResult =>
        new(Success: true, ExitCode: 0, StdErr: string.Empty, Duration: TimeSpan.Zero, Error: null);

    private static OutputPlan HlsOutputPlan() =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

    private static FinalizeInput MakeInput(
        OutputPlan? plan = null,
        HlsDerivatives? hlsDerivatives = null
    ) =>
        new(
            Results: [SuccessResult],
            Plan: plan ?? HlsOutputPlan(),
            OutputDirectory: "out",
            MediaTitle: "Test",
            HlsDerivatives: hlsDerivatives
        );

    private static (
        FinalizeStage Stage,
        Mock<IChapterWriter> ChapterMock,
        Mock<IFontExtractor> FontMock,
        TestStorage Storage
    ) BuildStage()
    {
        TestStorage storage = new();

        Mock<IOutputStrategy> strategyMock = new();
        strategyMock
            .Setup(s =>
                s.FinalizeAsync(
                    It.IsAny<string>(),
                    It.IsAny<OutputPlan>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        strategyMock.Setup(s => s.Format).Returns(OutputFormat.Hls);

        Mock<IOutputStrategyFactory> factoryMock = new();
        factoryMock.Setup(f => f.Resolve(It.IsAny<OutputFormat>())).Returns(strategyMock.Object);

        Mock<IChapterWriter> chapterMock = new();
        chapterMock
            .Setup(c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        Mock<IFontExtractor> fontMock = new();
        fontMock
            .Setup(f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        FinalizeStage stage = new(
            chapterMock.Object,
            fontMock.Object,
            factoryMock.Object,
            NullLogger<FinalizeStage>.Instance,
            storage
        );

        return (stage, chapterMock, fontMock, storage);
    }

    private static EncodingContext ContextWithChapters(int chapterCount = 2)
    {
        ChapterInfo[] chapters = Enumerable
            .Range(0, chapterCount)
            .Select(i => new ChapterInfo(
                TimeSpan.FromMinutes(i),
                TimeSpan.FromMinutes(i + 1),
                $"Chapter {i + 1}"
            ))
            .ToArray();

        return EncodingContext.Create() with
        {
            MediaInfo = new MediaInfo(
                FilePath: "/src/movie.mkv",
                Format: "matroska",
                Duration: TimeSpan.FromHours(2),
                OverallBitRateKbps: 8000,
                FileSizeBytes: 7_200_000_000L,
                VideoStreams: [],
                AudioStreams: [],
                SubtitleStreams: [],
                Chapters: chapters
            ),
        };
    }

    // ── GenerateChapters = true → chapter writer called when chapters exist ───

    [Fact]
    public async Task GenerateChapters_True_CallsChapterWriter_WhenChaptersExist()
    {
        (FinalizeStage stage, Mock<IChapterWriter> chapterMock, _, _) = BuildStage();
        FinalizeInput input = MakeInput(
            hlsDerivatives: new HlsDerivatives { GenerateChapters = true }
        );
        EncodingContext ctx = ContextWithChapters(2);

        await stage.ExecuteAsync(input, ctx, CancellationToken.None);

        chapterMock.Verify(
            c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GenerateChapters_False_SkipsChapterWriter()
    {
        (FinalizeStage stage, Mock<IChapterWriter> chapterMock, _, _) = BuildStage();
        FinalizeInput input = MakeInput(
            hlsDerivatives: new HlsDerivatives { GenerateChapters = false }
        );
        EncodingContext ctx = ContextWithChapters(2);

        await stage.ExecuteAsync(input, ctx, CancellationToken.None);

        chapterMock.Verify(
            c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── GenerateFontsJson = true → font extractor called ─────────────────────

    [Fact]
    public async Task GenerateFontsJson_True_CallsFontExtractor()
    {
        (FinalizeStage stage, _, Mock<IFontExtractor> fontMock, _) = BuildStage();
        FinalizeInput input = MakeInput(
            hlsDerivatives: new HlsDerivatives { GenerateFontsJson = true }
        );

        await stage.ExecuteAsync(input, EncodingContext.Create(), CancellationToken.None);

        fontMock.Verify(
            f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GenerateFontsJson_False_SkipsFontExtractor()
    {
        (FinalizeStage stage, _, Mock<IFontExtractor> fontMock, _) = BuildStage();
        FinalizeInput input = MakeInput(
            hlsDerivatives: new HlsDerivatives { GenerateFontsJson = false }
        );

        await stage.ExecuteAsync(input, EncodingContext.Create(), CancellationToken.None);

        fontMock.Verify(
            f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── HlsDerivatives = null → defaults apply (both generators called) ───────

    [Fact]
    public async Task NullHlsDerivatives_UsesDefaults_BothGeneratorsCalled()
    {
        (FinalizeStage stage, Mock<IChapterWriter> chapterMock, Mock<IFontExtractor> fontMock, _) =
            BuildStage();
        FinalizeInput input = MakeInput(hlsDerivatives: null);
        EncodingContext ctx = ContextWithChapters(2);

        await stage.ExecuteAsync(input, ctx, CancellationToken.None);

        // GenerateChapters defaults to true
        chapterMock.Verify(
            c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // GenerateFontsJson defaults to true
        fontMock.Verify(
            f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── GenerateChapters = true but no chapters → writer not called ───────────

    [Fact]
    public async Task GenerateChapters_True_NoChaptersInSource_SkipsWriter()
    {
        (FinalizeStage stage, Mock<IChapterWriter> chapterMock, _, _) = BuildStage();
        FinalizeInput input = MakeInput(
            hlsDerivatives: new HlsDerivatives { GenerateChapters = true }
        );
        // MediaInfo with zero chapters
        EncodingContext ctx = ContextWithChapters(0);

        await stage.ExecuteAsync(input, ctx, CancellationToken.None);

        chapterMock.Verify(
            c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── Flags without generators execute without throwing ─────────────────────
    // (GenerateSpriteVtt, GenerateIFramePlaylists, ExtractClosedCaptions,
    //  GenerateThumbnailTrack, WriteOriginalFilename are no-ops / FFmpeg-side)

    [Fact]
    public async Task NoOpFlags_DoNotThrow()
    {
        (FinalizeStage stage, _, _, _) = BuildStage();
        FinalizeInput input = MakeInput(
            hlsDerivatives: new HlsDerivatives
            {
                GenerateSpriteVtt = false,
                GenerateIFramePlaylists = true,
                ExtractClosedCaptions = true,
                GenerateThumbnailTrack = false,
                WriteOriginalFilename = false,
                GenerateMetadataJson = false,
            }
        );

        Func<Task> act = () =>
            stage.ExecuteAsync(input, EncodingContext.Create(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
