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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Pipeline.Stages;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Metadata;

/// <summary>
/// Integration tests: BuildStage injects IMetadataInjector → rendered FFmpeg
/// command contains expected -metadata flags.
/// </summary>
public class MetadataInjectorBuildStageIntegrationTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static BuildStage CreateStageWithInjector(IMetadataInjector injector) =>
        new(
            new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            fontExtractor: new FontExtractor(TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal(),
            metadataInjector: injector
        );

    private static ExecutionPlan BuildMkvPlan() =>
        new(
            [
                new(
                    "g0",
                    [
                        new("decode_0", OperationType.Decode, [], new()),
                        new("encode_0", OperationType.Encode, ["decode_0"], new()),
                    ],
                    null,
                    0,
                    2,
                    false,
                    1
                ),
            ],
            TimeSpan.FromMinutes(90),
            new(
                OutputFormat.Mkv,
                [
                    new(
                        1920,
                        1080,
                        "libx264",
                        23,
                        0,
                        "medium",
                        "high",
                        "4.1",
                        false,
                        "yuv420p",
                        "0:v:0",
                        new()
                    ),
                ],
                [
                    new(
                        "aac",
                        192,
                        2,
                        48000,
                        StreamAction.Transcode,
                        "en",
                        "0:a:0"
                    ),
                ],
                [],
                null
            )
        );

    // -----------------------------------------------------------------------
    // F2 integration: BuildStage passes context-MediaItem metadata to injector
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_WithMovieMediaItem_CommandContainsMetadataTitleFlag()
    {
        MetadataInjector injector = new();
        BuildStage stage = CreateStageWithInjector(injector);

        // EnableMetadataInjection must be explicitly turned on — MediaItem alone
        // no longer activates injection (see the contract note above the class).
        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(
                MediaType.Movie,
                550,
                "Fight Club",
                1999
            ),
            EnableMetadataInjection = true,
        };

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(
            plan,
            "/movies/fight_club.mkv",
            "/tmp/nmtest-output/fc",
            "Fight Club.NoMercy"
        );

        StageResult result = await stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands.Should().NotBeEmpty();

        string[] args = commands[0].Arguments;
        ContainsPair(args, "-metadata", "title=Fight Club")
            .Should()
            .BeTrue("BuildStage must forward -metadata title from MediaItem");
        ContainsPair(args, "-metadata", "year=1999")
            .Should()
            .BeTrue("BuildStage must forward -metadata year from MediaItem");
    }

    [Fact]
    public async Task BuildStage_WithEpisodeMediaItem_CommandContainsShowAndEpisodeFlags()
    {
        MetadataInjector injector = new();
        BuildStage stage = CreateStageWithInjector(injector);

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new EpisodeMediaRef(
                MediaType.Episode,
                62085,
                "Pilot",
                2008,
                "Breaking Bad",
                1,
                1
            ),
            EnableMetadataInjection = true,
        };

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(
            plan,
            "/tv/breaking_bad_s01e01.mkv",
            "/tmp/nmtest-output/bb",
            "Pilot.NoMercy"
        );

        StageResult result = await stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        ContainsPair(args, "-metadata", "show=Breaking Bad")
            .Should()
            .BeTrue("expected -metadata show=");
        ContainsPair(args, "-metadata", "season_number=1")
            .Should()
            .BeTrue("expected -metadata season_number=");
        ContainsPair(args, "-metadata", "episode_id=1")
            .Should()
            .BeTrue("expected -metadata episode_id=");
    }

    [Fact]
    public async Task BuildStage_NullMediaItem_NoMetadataFlagsEmitted()
    {
        MetadataInjector injector = new();
        BuildStage stage = CreateStageWithInjector(injector);

        EncodingContext context = EncodingContext.Create();

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");

        StageResult result = await stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().NotContain("-metadata", "no -metadata flags when context has no MediaItem");
    }

    [Fact]
    public async Task BuildStage_MediaItemSetButInjectionDisabled_NoMetadataFlagsEmitted()
    {
        // The regression this slice fixes: MediaItem is now attached to every
        // production encode request (to drive manifest/reconstruction writes),
        // so MediaItem alone must NEVER be enough to trigger -metadata injection.
        // EnableMetadataInjection is the only signal allowed to do that, and it
        // defaults to false on every request VideoEncodeJob builds today.
        MetadataInjector injector = new();
        BuildStage stage = CreateStageWithInjector(injector);

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(
                MediaType.Movie,
                550,
                "Fight Club",
                1999
            ),
            // EnableMetadataInjection intentionally left at its default (false).
        };

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(plan, "/movies/test.mkv", "/tmp/nmtest-output/test", "Test.NoMercy");

        StageResult result = await stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should()
            .NotContain(
                "-metadata",
                "MediaItem is pure identity — it must not inject -metadata unless EnableMetadataInjection is explicitly true"
            );
    }

    // -----------------------------------------------------------------------
    // F2 DI: IMetadataInjector resolves from the container
    // -----------------------------------------------------------------------

    [Fact]
    public void DI_IMetadataInjector_Resolves()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddNoMercyEncoder(opts =>
        {
            opts.FfmpegPathOverride = "ffmpeg";
            opts.FfprobePathOverride = "ffprobe";
        });
        ServiceProvider provider = services.BuildServiceProvider();

        IMetadataInjector injector = provider.GetRequiredService<IMetadataInjector>();
        injector.Should().NotBeNull();
        injector.Should().BeOfType<MetadataInjector>();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static bool ContainsPair(string[] args, string flag, string value)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag && args[i + 1] == value)
                return true;
        }
        return false;
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }
}
