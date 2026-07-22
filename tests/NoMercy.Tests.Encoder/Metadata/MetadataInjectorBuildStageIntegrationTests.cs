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
            options: new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal(),
            metadataInjector: injector
        );

    private static ExecutionPlan BuildMkvPlan() =>
        new(
            Groups:
            [
                new(
                    GroupId: "g0",
                    Nodes:
                    [
                        new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new()),
                        new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: ["decode_0"], Parameters: new()),
                    ],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 2,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(minutes: 90),
            OutputPlan: new(
                Format: OutputFormat.Mkv,
                VideoOutputs:
                [
                    new(
                        Width: 1920,
                        Height: 1080,
                        EncoderName: "libx264",
                        Crf: 23,
                        BitrateKbps: 0,
                        Preset: "medium",
                        Profile: "high",
                        Level: "4.1",
                        TenBit: false,
                        PixelFormat: "yuv420p",
                        MapLabel: "0:v:0",
                        ExtraFlags: new()
                    ),
                ],
                AudioOutputs:
                [
                    new(
                        EncoderName: "aac",
                        BitrateKbps: 192,
                        Channels: 2,
                        SampleRate: 48000,
                        Action: StreamAction.Transcode,
                        Language: "en",
                        MapLabel: "0:a:0"
                    ),
                ],
                SubtitleOutputs: [],
                Thumbnails: null
            )
        );

    // -----------------------------------------------------------------------
    // F2 integration: BuildStage passes context-MediaItem metadata to injector
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_WithMovieMediaItem_CommandContainsMetadataTitleFlag()
    {
        MetadataInjector injector = new();
        BuildStage stage = CreateStageWithInjector(injector: injector);

        // EnableMetadataInjection must be explicitly turned on — MediaItem alone
        // no longer activates injection (see the contract note above the class).
        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(
                Type: MediaType.Movie,
                Id: 550,
                Title: "Fight Club",
                Year: 1999
            ),
            EnableMetadataInjection = true,
        };

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(
            Plan: plan,
            InputPath: "/movies/fight_club.mkv",
            OutputDirectory: "/tmp/nmtest-output/fc",
            MediaTitle: "Fight Club.NoMercy"
        );

        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands.Should().NotBeEmpty();

        string[] args = commands[0].Arguments;
        ContainsPair(args: args, flag: "-metadata", value: "title=Fight Club")
            .Should()
            .BeTrue(because: "BuildStage must forward -metadata title from MediaItem");
        ContainsPair(args: args, flag: "-metadata", value: "year=1999")
            .Should()
            .BeTrue(because: "BuildStage must forward -metadata year from MediaItem");
    }

    [Fact]
    public async Task BuildStage_WithEpisodeMediaItem_CommandContainsShowAndEpisodeFlags()
    {
        MetadataInjector injector = new();
        BuildStage stage = CreateStageWithInjector(injector: injector);

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new EpisodeMediaRef(
                Type: MediaType.Episode,
                Id: 62085,
                Title: "Pilot",
                Year: 2008,
                ShowTitle: "Breaking Bad",
                SeasonNumber: 1,
                EpisodeNumber: 1
            ),
            EnableMetadataInjection = true,
        };

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(
            Plan: plan,
            InputPath: "/tv/breaking_bad_s01e01.mkv",
            OutputDirectory: "/tmp/nmtest-output/bb",
            MediaTitle: "Pilot.NoMercy"
        );

        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        ContainsPair(args: args, flag: "-metadata", value: "show=Breaking Bad")
            .Should()
            .BeTrue(because: "expected -metadata show=");
        ContainsPair(args: args, flag: "-metadata", value: "season_number=1")
            .Should()
            .BeTrue(because: "expected -metadata season_number=");
        ContainsPair(args: args, flag: "-metadata", value: "episode_id=1")
            .Should()
            .BeTrue(because: "expected -metadata episode_id=");
    }

    [Fact]
    public async Task BuildStage_NullMediaItem_NoMetadataFlagsEmitted()
    {
        MetadataInjector injector = new();
        BuildStage stage = CreateStageWithInjector(injector: injector);

        EncodingContext context = EncodingContext.Create();

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");

        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should().NotContain(unexpected: "-metadata", because: "no -metadata flags when context has no MediaItem");
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
        BuildStage stage = CreateStageWithInjector(injector: injector);

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(
                Type: MediaType.Movie,
                Id: 550,
                Title: "Fight Club",
                Year: 1999
            ),
            // EnableMetadataInjection intentionally left at its default (false).
        };

        ExecutionPlan plan = BuildMkvPlan();
        BuildInput input = new(Plan: plan, InputPath: "/movies/test.mkv", OutputDirectory: "/tmp/nmtest-output/test", MediaTitle: "Test.NoMercy");

        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        string[] args = commands[0].Arguments;

        args.Should()
            .NotContain(
                unexpected: "-metadata",
                because: "MediaItem is pure identity — it must not inject -metadata unless EnableMetadataInjection is explicitly true"
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
        services.AddNoMercyEncoder(configure: opts =>
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
