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
using Newtonsoft.Json;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Integration test: FinalizeStage emits the single per-media-item
/// <c>.nomercy.json</c> blueprint at the media folder root — replaces the old
/// per-preset <c>manifest.json</c> + <c>reconstruction.json</c> pair. See
/// <c>.claude/specs/reconstruction-blueprint/SPEC.md</c>.
/// </summary>
public class FinalizeStageBlueprintTests
{
    private static BundleLayout MakeHlsLayout(string presetSlug = "web-1080p") =>
        new(
            MediaKey: "mfa",
            PresetSlug: presetSlug,
            IsSingleFile: false,
            BundleDirectory: $"encodes/{presetSlug}",
            MasterPlaylistName: "mfa_master.m3u8",
            ManifestPath: $"encodes/{presetSlug}/manifest.json",
            ReconstructionPath: $"encodes/{presetSlug}/reconstruction.json",
            SingleFileName: string.Empty,
            PresetId: $"01HZ-{presetSlug}",
            PresetName: presetSlug,
            ContainerString: "hls-fmp4"
        );

    private static OutputPlan MakeOutputPlan(BundleLayout layout) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Layout: layout
        );

    private static FinalizeInput MakeFinalizeInput(OutputPlan plan, string outputDir) =>
        new(
            Results:
            [
                new(
                    Success: true,
                    ExitCode: 0,
                    StdErr: string.Empty,
                    Duration: TimeSpan.Zero,
                    Error: null
                ),
            ],
            Plan: plan,
            OutputDirectory: outputDir,
            MediaTitle: "Fight Club"
        );

    private static MediaInfo MinimalMediaInfo() =>
        new(
            FilePath: "/media/fight-club.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromSeconds(seconds: 7800),
            OverallBitRateKbps: 25_000,
            FileSizeBytes: 24_375_000_000L,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: [],
            Attachments: []
        );

    private static FinalizeStage MakeStage(
        TestStorage storage,
        IMediaBlueprintWriter? blueprintWriter
    )
    {
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
        factoryMock.Setup(expression: f => f.Resolve(OutputFormat.Hls)).Returns(value: strategyMock.Object);

        Mock<IChapterWriter> chapterWriterMock = new();
        Mock<IFontExtractor> fontExtractorMock = new();
        fontExtractorMock
            .Setup(expression: f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: 0);

        return new(
            chapterWriter: chapterWriterMock.Object,
            fontExtractor: fontExtractorMock.Object,
            outputStrategyFactory: factoryMock.Object,
            logger: NullLogger<FinalizeStage>.Instance,
            storage: storage,
            blueprintWriter: blueprintWriter
        );
    }

    [Fact]
    public async Task FinalizeStage_EmitsBlueprintJson_AtTheMediaFolderRoot()
    {
        TestStorage storage = new();
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout: layout);
        string outputDir = "Fight Club (1999)";

        storage.Seed(path: $"{outputDir}/mfa_master.m3u8", bytes: [0x23, 0x45]);
        storage.Seed(path: $"{outputDir}/video/1080p/mfa_1080p_init.mp4", bytes: [0x00, 0x01]);

        FinalizeStage stage = MakeStage(
            storage: storage,
            blueprintWriter: new MediaBlueprintWriter(builder: new MediaBlueprintBuilder())
        );

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999),
            MediaInfo = MinimalMediaInfo(),
        };

        StageResult result = await stage.ExecuteAsync(
            input: MakeFinalizeInput(plan: plan, outputDir: outputDir),
            context: context,
            ct: CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();

        // At the media folder root — never nested under encodes/{slug}/.
        string? json = storage.ReadString(path: $"{outputDir}/{MediaBlueprintWriter.FileName}");
        json.Should().NotBeNull(because: ".nomercy.json must be written at the media folder root");

        MediaBlueprint? blueprint = JsonConvert.DeserializeObject<MediaBlueprint>(value: json!);
        blueprint.Should().NotBeNull();
        blueprint!.Identity.Type.Should().Be(expected: "movie");
        blueprint.Identity.TmdbId.Should().Be(expected: 550);
        blueprint.Source.Container.Should().Be(expected: "matroska");
        blueprint.Encodes.Should().ContainSingle();
        blueprint.Encodes[index: 0].PresetSlug.Should().Be(expected: "web-1080p");
        blueprint.Encodes[index: 0].TargetContainer.Should().Be(expected: "matroska");
    }

    [Fact]
    public async Task FinalizeStage_WithoutBlueprintWriter_StillSucceeds()
    {
        // Regression guard: when IMediaBlueprintWriter is null (legacy caller),
        // finalize still succeeds and no exception is thrown.
        TestStorage storage = new();
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout: layout);
        string outputDir = "Fight Club (1999)";

        storage.Seed(path: $"{outputDir}/mfa_master.m3u8", bytes: [0x23]);

        FinalizeStage stage = MakeStage(storage: storage, blueprintWriter: null);

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999),
            MediaInfo = MinimalMediaInfo(),
        };

        StageResult result = await stage.ExecuteAsync(
            input: MakeFinalizeInput(plan: plan, outputDir: outputDir),
            context: context,
            ct: CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();
        storage.ReadString(path: $"{outputDir}/{MediaBlueprintWriter.FileName}").Should().BeNull();
    }

    [Fact]
    public async Task Blueprint_IsWrittenIntoStaging_SoPublishCarriesIt_AndNamesTheDestination()
    {
        // Every encode runs in a staging temp dir; PublishTempDirAsync sweeps that
        // dir's contents to the real folder afterward. The blueprint MUST be written
        // into staging (alongside video_*/, audio_*/) so it publishes exactly like a
        // rendition — writing it to the destination directly through the staging
        // storage lands it at a stray path that publish never sees, and the file
        // never reaches the media folder. Its output_location still names the real
        // destination.
        TestStorage storage = new();
        const string stagingDir = "cache/encoder/Fight Club (1999)";
        const string destination = "Films/Fight Club (1999)";
        const string sourceMkv = "Films/Fight Club (1999)/Fight Club (1999).mkv";
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout: layout);

        storage.Seed(path: $"{stagingDir}/mfa_master.m3u8", bytes: [0x23, 0x45]);

        FinalizeStage stage = MakeStage(
            storage: storage,
            blueprintWriter: new MediaBlueprintWriter(builder: new MediaBlueprintBuilder())
        );

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999),
            MediaInfo = MinimalMediaInfo(),
            OriginalOutputDirectory = destination,
            OriginalInputPath = sourceMkv,
        };

        StageResult result = await stage.ExecuteAsync(
            input: MakeFinalizeInput(plan: plan, outputDir: stagingDir),
            context: context,
            ct: CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();

        // Written INTO the staging dir (publish relocates it to the media root).
        string? json = storage.ReadString(path: $"{stagingDir}/{MediaBlueprintWriter.FileName}");
        json.Should().NotBeNull(because: ".nomercy.json must be written into staging so publish carries it");

        MediaBlueprint blueprint = JsonConvert.DeserializeObject<MediaBlueprint>(value: json!)!;
        // The record names the real destination, not the staging scaffolding.
        blueprint.Encodes[index: 0].OutputLocation.Should().Be(expected: destination);
        // And the real source, not the staging lease MediaInfo.FilePath points at.
        blueprint.Source.Path.Should().Be(expected: sourceMkv);
    }

    [Fact]
    public async Task Blueprint_MergesASecondPresetEncode_WithoutDroppingTheFirst()
    {
        TestStorage storage = new();
        string outputDir = "Fight Club (1999)";
        IMediaBlueprintWriter writer = new MediaBlueprintWriter(builder: new MediaBlueprintBuilder());

        storage.Seed(path: $"{outputDir}/mfa_master.m3u8", bytes: [0x23]);

        FinalizeStage firstStage = MakeStage(storage: storage, blueprintWriter: writer);
        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(Type: MediaType.Movie, Id: 550, Title: "Fight Club", Year: 1999),
            MediaInfo = MinimalMediaInfo(),
        };

        await firstStage.ExecuteAsync(
            input: MakeFinalizeInput(plan: MakeOutputPlan(layout: MakeHlsLayout(presetSlug: "web-1080p")), outputDir: outputDir),
            context: context,
            ct: CancellationToken.None
        );

        FinalizeStage secondStage = MakeStage(storage: storage, blueprintWriter: writer);
        await secondStage.ExecuteAsync(
            input: MakeFinalizeInput(plan: MakeOutputPlan(layout: MakeHlsLayout(presetSlug: "archive-mkv")), outputDir: outputDir),
            context: context,
            ct: CancellationToken.None
        );

        MediaBlueprint blueprint = JsonConvert.DeserializeObject<MediaBlueprint>(
            value: storage.ReadString(path: $"{outputDir}/{MediaBlueprintWriter.FileName}")!
        )!;

        blueprint.Encodes.Should().HaveCount(expected: 2);
        blueprint
            .Encodes.Select(selector: e => e.PresetSlug)
            .Should()
            .BeEquivalentTo(expectation: ["web-1080p", "archive-mkv"]);
        // Identity + source stay shared, written once.
        blueprint.Identity.TmdbId.Should().Be(expected: 550);
    }
}
