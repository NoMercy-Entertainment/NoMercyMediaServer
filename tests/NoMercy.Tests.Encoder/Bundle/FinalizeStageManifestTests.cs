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
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Integration test: FinalizeStage emits manifest.json at BundleLayout.ManifestPath
/// after a successful encode.
/// </summary>
public class FinalizeStageManifestTests
{
    private static BundleLayout MakeHlsLayout() =>
        new(
            MediaKey: "mfa",
            PresetSlug: "web-1080p",
            IsSingleFile: false,
            BundleDirectory: "encodes/web-1080p",
            MasterPlaylistName: "mfa_master.m3u8",
            ManifestPath: "encodes/web-1080p/manifest.json",
            ReconstructionPath: "encodes/web-1080p/reconstruction.json",
            SingleFileName: string.Empty,
            PresetId: "01HZABC123",
            PresetName: "Web 1080p",
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

    private static FinalizeStage MakeStage(TestStorage storage)
    {
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
        factoryMock.Setup(f => f.Resolve(OutputFormat.Hls)).Returns(strategyMock.Object);

        Mock<IFontExtractor> fontExtractorMock = new();
        fontExtractorMock
            .Setup(f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return new(
            new Mock<IChapterWriter>().Object,
            fontExtractorMock.Object,
            factoryMock.Object,
            NullLogger<FinalizeStage>.Instance,
            storage,
            new BundleManifestWriter()
        );
    }

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

    [Fact]
    public async Task FinalizeStage_EmitsManifestJson_AtLayoutManifestPath()
    {
        TestStorage storage = new();
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout);
        // The real per-encode OutputDirectory is the media item's OWN folder —
        // distinct from layout.BundleDirectory, which is a preset-scoped
        // sub-directory nested INSIDE it (see FinalizeStage.WriteManifestAsync).
        string outputDir = "Fight Club (1999)";

        // Seed a couple of HLS files so the manifest has a non-empty file list.
        storage.Seed($"{outputDir}/mfa_master.m3u8", [0x23, 0x45]);
        storage.Seed($"{outputDir}/video/1080p/mfa_1080p_init.mp4", [0x00, 0x01]);

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
        factoryMock.Setup(f => f.Resolve(OutputFormat.Hls)).Returns(strategyMock.Object);

        Mock<IChapterWriter> chapterWriterMock = new();
        Mock<IFontExtractor> fontExtractorMock = new();
        fontExtractorMock
            .Setup(f => f.WriteFontManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        BundleManifestWriter manifestWriter = new();

        FinalizeStage stage = new(
            chapterWriterMock.Object,
            fontExtractorMock.Object,
            factoryMock.Object,
            NullLogger<FinalizeStage>.Instance,
            storage,
            manifestWriter
        );

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new(MediaType.Movie, 550, "Fight Club", 1999),
        };

        StageResult result = await stage.ExecuteAsync(
            MakeFinalizeInput(plan, outputDir),
            context,
            CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();

        // Nested under the media item's own OutputDirectory — see
        // FinalizeStage.WriteManifestAsync for why this isn't layout.ManifestPath as-is.
        string? json = storage.ReadString($"{outputDir}/{layout.ManifestPath}");
        json.Should().NotBeNull("manifest.json must be written under OutputDirectory/ManifestPath");

        BundleManifest? manifest = JsonConvert.DeserializeObject<BundleManifest>(json!);
        manifest.Should().NotBeNull();
        manifest!.MediaKey.Should().Be("mfa");
        manifest.PresetSlug.Should().Be("web-1080p");
        manifest.PresetId.Should().Be("01HZABC123");
        manifest.PresetName.Should().Be("Web 1080p");
        manifest.Container.Should().Be("hls-fmp4");
        manifest.MediaType.Should().Be("movie");
        manifest.MediaId.Should().Be(550);
        manifest.CompletedAt.Should().NotBeNull();
        manifest.Files.Should().NotBeEmpty();
    }

    // ── What the manifest says about where things are ────────────────────────
    // An encode is assembled in a staging directory and published to the media
    // folder, after which the staging directory is deleted. A manifest naming it
    // describes a place that no longer exists.

    [Fact]
    public async Task Manifest_NamesTheDestination_NotTheStagingDirectoryItWasAssembledIn()
    {
        TestStorage storage = new();
        const string stagingDir = "cache/encoder/Fight Club (1999)";
        const string destination = "Films/Fight Club (1999)";
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout);

        storage.Seed($"{stagingDir}/mfa_master.m3u8", [0x23, 0x45]);
        storage.Seed($"{stagingDir}/video/1080p/mfa_1080p_init.mp4", [0x00, 0x01]);

        FinalizeStage stage = MakeStage(storage);

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new(MediaType.Movie, 550, "Fight Club", 1999),
            OriginalOutputDirectory = destination,
        };

        StageResult result = await stage.ExecuteAsync(
            MakeFinalizeInput(plan, stagingDir),
            context,
            CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();

        BundleManifest manifest = ReadManifest(storage, stagingDir, layout);
        manifest
            .MediaFolder.Should()
            .Be(destination, "the staging directory is deleted once the encode publishes");
    }

    [Fact]
    public async Task Manifest_ListsFilesRelativeToTheMediaFolder()
    {
        // The encoder writes video_*/ and the master playlist at the media folder
        // root, not inside the bundle directory. Measuring against the bundle
        // directory left every entry absolute — and absolute into staging at that.
        TestStorage storage = new();
        const string outputDir = "Films/Fight Club (1999)";
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout);

        storage.Seed($"{outputDir}/mfa_master.m3u8", [0x23, 0x45]);
        storage.Seed($"{outputDir}/video/1080p/mfa_1080p_init.mp4", [0x00, 0x01]);

        FinalizeStage stage = MakeStage(storage);

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new(MediaType.Movie, 550, "Fight Club", 1999),
        };

        StageResult result = await stage.ExecuteAsync(
            MakeFinalizeInput(plan, outputDir),
            context,
            CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();

        BundleManifest manifest = ReadManifest(storage, outputDir, layout);
        manifest.Files.Should().Contain("mfa_master.m3u8");
        manifest
            .Files.Should()
            .OnlyContain(f => !f.Contains(outputDir), "entries are relative to the media folder");
    }

    private static BundleManifest ReadManifest(
        TestStorage storage,
        string outputDir,
        BundleLayout layout
    )
    {
        string? json = storage.ReadString($"{outputDir}/{layout.ManifestPath}");
        json.Should().NotBeNull("manifest.json must be written under OutputDirectory/ManifestPath");
        BundleManifest? manifest = JsonConvert.DeserializeObject<BundleManifest>(json!);
        manifest.Should().NotBeNull();
        return manifest!;
    }
}
