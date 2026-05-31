using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using NoMercy.Encoder.Analysis;
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
/// Integration test: FinalizeStage emits both manifest.json and reconstruction.json
/// at their respective BundleLayout paths after a successful encode.
/// </summary>
public class FinalizeStageReconstructionTests
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

    private static FinalizeInput MakeFinalizeInput(OutputPlan plan, string outputDir) =>
        new(
            Results:
            [
                new ExecutionResult(
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
            Duration: TimeSpan.FromSeconds(7800),
            OverallBitRateKbps: 25_000,
            FileSizeBytes: 24_375_000_000L,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: [],
            Attachments: []
        );

    [Fact]
    public async Task FinalizeStage_EmitsBothManifestAndReconstructionJson()
    {
        TestStorage storage = new();
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout);
        string outputDir = layout.BundleDirectory;

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
            .Returns(Task.CompletedTask);

        BundleManifestWriter manifestWriter = new(storage);
        ReconstructionWriter reconstructionWriter = new();

        FinalizeStage stage = new(
            chapterWriterMock.Object,
            fontExtractorMock.Object,
            factoryMock.Object,
            NullLogger<FinalizeStage>.Instance,
            storage,
            manifestWriter,
            reconstructionWriter
        );

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MediaItemRef(MediaType.Movie, 550, "Fight Club", 1999),
            MediaInfo = MinimalMediaInfo(),
        };

        StageResult result = await stage.ExecuteAsync(
            MakeFinalizeInput(plan, outputDir),
            context,
            CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();

        // manifest.json must be present
        string? manifestJson = storage.ReadString(layout.ManifestPath);
        manifestJson.Should().NotBeNull("manifest.json must be written to ManifestPath");

        BundleManifest? manifest = JsonConvert.DeserializeObject<BundleManifest>(manifestJson!);
        manifest.Should().NotBeNull();
        manifest!.MediaKey.Should().Be("mfa");

        // reconstruction.json must also be present
        string? reconstructionJson = storage.ReadString(layout.ReconstructionPath);
        reconstructionJson
            .Should()
            .NotBeNull("reconstruction.json must be written to ReconstructionPath");

        Reconstruction? reconstruction = JsonConvert.DeserializeObject<Reconstruction>(
            reconstructionJson!
        );
        reconstruction.Should().NotBeNull();
        reconstruction!.Version.Should().Be(1);
        reconstruction.Source.OriginalPath.Should().Be("/media/fight-club.mkv");
        reconstruction.Source.Container.Should().Be("matroska");
        reconstruction.TargetContainer.Should().Be("hls-fmp4");
    }

    [Fact]
    public async Task FinalizeStage_WithoutReconstructionWriter_StillEmitsManifest()
    {
        // Regression guard: when IReconstructionWriter is null (legacy caller),
        // manifest still writes and no exception is thrown.
        TestStorage storage = new();
        BundleLayout layout = MakeHlsLayout();
        OutputPlan plan = MakeOutputPlan(layout);
        string outputDir = layout.BundleDirectory;

        storage.Seed($"{outputDir}/mfa_master.m3u8", [0x23]);

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
            .Returns(Task.CompletedTask);

        BundleManifestWriter manifestWriter = new(storage);

        // No reconstructionWriter passed — optional parameter defaults to null
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
            MediaItem = new MediaItemRef(MediaType.Movie, 550, "Fight Club", 1999),
        };

        StageResult result = await stage.ExecuteAsync(
            MakeFinalizeInput(plan, outputDir),
            context,
            CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FinalizeOutput>>();
        storage.ReadString(layout.ManifestPath).Should().NotBeNull();
        storage.ReadString(layout.ReconstructionPath).Should().BeNull("no writer registered");
    }
}
