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
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;
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
            .Setup(expression: s =>
                s.FinalizeAsync(
                    It.IsAny<string>(),
                    It.IsAny<OutputPlan>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);
        strategyMock.Setup(expression: s => s.Format).Returns(value: OutputFormat.Hls);

        Mock<IOutputStrategyFactory> factoryMock = new();
        factoryMock.Setup(expression: f => f.Resolve(It.IsAny<OutputFormat>())).Returns(value: strategyMock.Object);

        Mock<IChapterWriter> chapterMock = new();
        chapterMock
            .Setup(expression: c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        Mock<IFontExtractor> fontMock = new();
        fontMock
            .Setup(expression: f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: 0);

        FinalizeStage stage = new(
            chapterWriter: chapterMock.Object,
            fontExtractor: fontMock.Object,
            outputStrategyFactory: factoryMock.Object,
            logger: NullLogger<FinalizeStage>.Instance,
            storage: storage
        );

        return (stage, chapterMock, fontMock, storage);
    }

    private static EncodingContext ContextWithChapters(int chapterCount = 2)
    {
        ChapterInfo[] chapters = Enumerable
            .Range(start: 0, count: chapterCount)
            .Select(selector: i => new ChapterInfo(
                Start: TimeSpan.FromMinutes(minutes: i),
                End: TimeSpan.FromMinutes(minutes: i + 1),
                Title: $"Chapter {i + 1}"
            ))
            .ToArray();

        return EncodingContext.Create() with
        {
            MediaInfo = new(
                FilePath: "/src/movie.mkv",
                Format: "matroska",
                Duration: TimeSpan.FromHours(hours: 2),
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
        FinalizeInput input = MakeInput(hlsDerivatives: new() { GenerateChapters = true });
        EncodingContext ctx = ContextWithChapters(chapterCount: 2);

        await stage.ExecuteAsync(input: input, context: ctx, ct: CancellationToken.None);

        chapterMock.Verify(
            expression: c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task GenerateChapters_False_SkipsChapterWriter()
    {
        (FinalizeStage stage, Mock<IChapterWriter> chapterMock, _, _) = BuildStage();
        FinalizeInput input = MakeInput(hlsDerivatives: new() { GenerateChapters = false });
        EncodingContext ctx = ContextWithChapters(chapterCount: 2);

        await stage.ExecuteAsync(input: input, context: ctx, ct: CancellationToken.None);

        chapterMock.Verify(
            expression: c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    // ── GenerateFontsJson = true → font extractor called ─────────────────────

    [Fact]
    public async Task GenerateFontsJson_True_CallsFontExtractor()
    {
        (FinalizeStage stage, _, Mock<IFontExtractor> fontMock, _) = BuildStage();
        FinalizeInput input = MakeInput(hlsDerivatives: new() { GenerateFontsJson = true });

        await stage.ExecuteAsync(input: input, context: EncodingContext.Create(), ct: CancellationToken.None);

        fontMock.Verify(
            expression: f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task GenerateFontsJson_False_SkipsFontExtractor()
    {
        (FinalizeStage stage, _, Mock<IFontExtractor> fontMock, _) = BuildStage();
        FinalizeInput input = MakeInput(hlsDerivatives: new() { GenerateFontsJson = false });

        await stage.ExecuteAsync(input: input, context: EncodingContext.Create(), ct: CancellationToken.None);

        fontMock.Verify(
            expression: f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    // ── HlsDerivatives = null → defaults apply (both generators called) ───────

    [Fact]
    public async Task NullHlsDerivatives_UsesDefaults_BothGeneratorsCalled()
    {
        (FinalizeStage stage, Mock<IChapterWriter> chapterMock, Mock<IFontExtractor> fontMock, _) =
            BuildStage();
        FinalizeInput input = MakeInput(hlsDerivatives: null);
        EncodingContext ctx = ContextWithChapters(chapterCount: 2);

        await stage.ExecuteAsync(input: input, context: ctx, ct: CancellationToken.None);

        // GenerateChapters defaults to true
        chapterMock.Verify(
            expression: c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );

        // GenerateFontsJson defaults to true
        fontMock.Verify(
            expression: f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    // ── GenerateChapters = true but no chapters → writer not called ───────────

    [Fact]
    public async Task GenerateChapters_True_NoChaptersInSource_SkipsWriter()
    {
        (FinalizeStage stage, Mock<IChapterWriter> chapterMock, _, _) = BuildStage();
        FinalizeInput input = MakeInput(hlsDerivatives: new() { GenerateChapters = true });
        // MediaInfo with zero chapters
        EncodingContext ctx = ContextWithChapters(chapterCount: 0);

        await stage.ExecuteAsync(input: input, context: ctx, ct: CancellationToken.None);

        chapterMock.Verify(
            expression: c =>
                c.WriteChaptersAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<ChapterInfo>>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
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
            hlsDerivatives: new()
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
            stage.ExecuteAsync(input: input, context: EncodingContext.Create(), ct: CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
