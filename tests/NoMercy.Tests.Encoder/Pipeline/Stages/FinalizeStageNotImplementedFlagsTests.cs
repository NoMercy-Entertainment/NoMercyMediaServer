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
/// FinalizeStage's HlsDerivatives feature flags include several "fail loudly"
/// guards for opt-ins that don't have an implementation yet
/// (GenerateIFramePlaylists, ExtractClosedCaptions). The existing
/// <see cref="FinalizeStageHlsDerivativesTests"/> only checks that the stage
/// doesn't throw — which is true because the outer try/catch converts every
/// exception into a <see cref="StageFailure"/>. These tests verify the
/// CONTRACT — the user-facing behaviour the dashboard sees on those flags:
///
/// • GenerateIFramePlaylists=true returns StageFailure with a message that
///   tells the operator the feature isn't wired.
/// • ExtractClosedCaptions=true returns StageFailure with the matching message.
/// • GenerateMasterPlaylist=false skips the output strategy's FinalizeAsync
///   entirely.
/// • All-false derivatives still finalize successfully (smoke check).
/// </summary>
public class FinalizeStageNotImplementedFlagsTests
{
    private static ExecutionResult SuccessResult =>
        new(true, 0, string.Empty, TimeSpan.Zero, null);

    private static OutputPlan HlsOutputPlan() =>
        new(
            OutputFormat.Hls,
            [],
            [],
            [],
            null
        );

    private static (
        FinalizeStage Stage,
        Mock<IOutputStrategy> StrategyMock,
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
        Mock<IFontExtractor> fontMock = new();
        fontMock
            .Setup(f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        FinalizeStage stage = new(
            chapterMock.Object,
            fontMock.Object,
            factoryMock.Object,
            NullLogger<FinalizeStage>.Instance,
            storage
        );

        return (stage, strategyMock, storage);
    }

    private static FinalizeInput MakeInput(HlsDerivatives derivatives) =>
        new(
            [SuccessResult],
            Plan: HlsOutputPlan(),
            OutputDirectory: "out",
            MediaTitle: "Test",
            HlsDerivatives: derivatives
        );

    // ── Not-implemented flags return StageFailure ────────────────────────────

    [Fact]
    public async Task GenerateIFramePlaylists_true_returns_StageFailure_with_diagnostic_message()
    {
        (FinalizeStage stage, _, _) = BuildStage();
        FinalizeInput input = MakeInput(new() { GenerateIFramePlaylists = true });

        StageResult result = await stage.ExecuteAsync(
            input,
            EncodingContext.Create(),
            CancellationToken.None
        );

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Message.Should().Contain("GenerateIFramePlaylists");
        // Source says "no IFramePlaylistGenerator is wired" — match either token.
        (
            failure.Error.Message.Contains("not wired")
            || failure.Error.Message.Contains("no IFramePlaylistGenerator")
        )
            .Should()
            .BeTrue($"unexpected error message: {failure.Error.Message}");
    }

    [Fact]
    public async Task ExtractClosedCaptions_true_returns_StageFailure_with_diagnostic_message()
    {
        (FinalizeStage stage, _, _) = BuildStage();
        FinalizeInput input = MakeInput(new() { ExtractClosedCaptions = true });

        StageResult result = await stage.ExecuteAsync(
            input,
            EncodingContext.Create(),
            CancellationToken.None
        );

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Message.Should().Contain("ExtractClosedCaptions");
        (
            failure.Error.Message.Contains("CcExtractor")
            || failure.Error.Message.Contains("not wired")
        )
            .Should()
            .BeTrue($"unexpected error message: {failure.Error.Message}");
    }

    // ── GenerateMasterPlaylist gating ────────────────────────────────────────

    [Fact]
    public async Task GenerateMasterPlaylist_false_skips_output_strategy_FinalizeAsync()
    {
        (FinalizeStage stage, Mock<IOutputStrategy> strategyMock, _) = BuildStage();
        FinalizeInput input = MakeInput(
            new()
            {
                GenerateMasterPlaylist = false,
                GenerateFontsJson = false,
                GenerateChapters = false,
            }
        );

        StageResult result = await stage.ExecuteAsync(
            input,
            EncodingContext.Create(),
            CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();
        strategyMock.Verify(
            s =>
                s.FinalizeAsync(
                    It.IsAny<string>(),
                    It.IsAny<OutputPlan>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GenerateMasterPlaylist_true_calls_output_strategy_FinalizeAsync()
    {
        (FinalizeStage stage, Mock<IOutputStrategy> strategyMock, _) = BuildStage();
        FinalizeInput input = MakeInput(
            new()
            {
                GenerateMasterPlaylist = true,
                GenerateFontsJson = false,
                GenerateChapters = false,
            }
        );

        await stage.ExecuteAsync(input, EncodingContext.Create(), CancellationToken.None);

        strategyMock.Verify(
            s =>
                s.FinalizeAsync(
                    It.IsAny<string>(),
                    It.IsAny<OutputPlan>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── Null HlsDerivatives defaults ─────────────────────────────────────────

    [Fact]
    public async Task Null_HlsDerivatives_uses_spec_defaults_not_skip_everything()
    {
        // null is documented as "use spec defaults" — meaning GenerateMasterPlaylist
        // / GenerateChapters / GenerateFontsJson default-true. The output strategy
        // FinalizeAsync MUST be called.
        (FinalizeStage stage, Mock<IOutputStrategy> strategyMock, _) = BuildStage();
        FinalizeInput input = new(
            [SuccessResult],
            Plan: HlsOutputPlan(),
            OutputDirectory: "out",
            MediaTitle: "Test",
            HlsDerivatives: null
        );

        await stage.ExecuteAsync(input, EncodingContext.Create(), CancellationToken.None);

        strategyMock.Verify(
            s =>
                s.FinalizeAsync(
                    It.IsAny<string>(),
                    It.IsAny<OutputPlan>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
