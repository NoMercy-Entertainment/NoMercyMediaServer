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
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Pipeline.Stages;
using NoMercy.Tests.Encoder.Storage;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline;

public class EncoderTests : IDisposable
{
    private readonly Mock<IMediaAnalyzer> _analyzer = new();
    private readonly Mock<IStorage> _storage = new();
    private readonly Mock<IFfmpegExecutor> _ffmpegExecutor = new();
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly List<string> _tempDirectories = [];

    private readonly NoMercy.Encoder.Pipeline.Encoder _encoder;

    public EncoderTests()
    {
        SetupDefaultHardware();
        SetupDefaultCodecResolver();

        EncoderOptions options = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };

        AnalyzeStage analyzeStage = new(
            analyzer: _analyzer.Object,
            storage: _storage.Object,
            logger: NullLogger<AnalyzeStage>.Instance
        );
        ValidateStage validateStage = new(logger: NullLogger<ValidateStage>.Instance);
        PlanStage planStage = new(
            graphBuilder: new(),
            groupingStrategy: new(),
            costEstimator: new(),
            codecResolver: _codecResolver.Object,
            hardware: _hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance,
            outputNamingResolver: new OutputNamingResolver(mediaKeys: new MediaKeyResolver())
        );
        OutputStrategyFactory outputFactory = new(strategies:
        [
            new HlsOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new MkvOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new Mp4OutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new DashOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new Mp3OutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new FlacOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new OggOutputStrategy(storage: TestStorageFactory.CreateLocal()),
        ]);
        BuildStage buildStage = new(
            options: options,
            fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: outputFactory,
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal(),
            metadataInjector: new MetadataInjector(),
            metadataMerger: new MetadataMerger()
        );
        ExecuteStage executeStage = new(
            executor: _ffmpegExecutor.Object,
            checkpointStore: new Mock<ICheckpointStore>().Object,
            logger: NullLogger<ExecuteStage>.Instance
        );
        FinalizeStage finalizeStage = new(
            chapterWriter: new ChapterWriter(storage: TestStorageFactory.CreateLocal()),
            fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
            outputStrategyFactory: outputFactory,
            logger: NullLogger<FinalizeStage>.Instance,
            storage: TestStorageFactory.CreateLocal(),
            blueprintWriter: new MediaBlueprintWriter(builder: new MediaBlueprintBuilder())
        );

        _encoder = new(
            analyzeStage: analyzeStage,
            validateStage: validateStage,
            planStage: planStage,
            buildStage: buildStage,
            executeStage: executeStage,
            finalizeStage: finalizeStage,
            logger: NullLogger<NoMercy.Encoder.Pipeline.Encoder>.Instance
        );
    }

    private void SetupDefaultHardware()
    {
        _hardware.Setup(expression: h => h.HasGpu).Returns(value: false);
        _hardware.Setup(expression: h => h.CpuCores).Returns(value: 8);
        _hardware.Setup(expression: h => h.Gpus).Returns(value: []);
        _hardware.Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(value: false);
        _hardware
            .Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns(value: (GpuDevice?)null);
    }

    private void SetupDefaultCodecResolver()
    {
        _codecResolver
            .Setup(expression: r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                value: new ResolvedCodec(
                    FfmpegEncoderName: "libx264",
                    EncoderInfo: new(
                        FfmpegName: "libx264",
                        RequiredVendor: null,
                        Presets: ["medium"],
                        Profiles: ["high"],
                        Levels: ["4.1"],
                        QualityRange: new(Min: 0, Max: 51, Default: 23),
                        SupportedRateControl: [RateControlMode.Crf],
                        Supports10Bit: false,
                        SupportsHdr: false,
                        MaxConcurrentSessions: int.MaxValue,
                        PixelFormat10Bit: "yuv420p10le",
                        VendorSpecificFlags: new()
                    ),
                    Device: null,
                    DefaultRateControl: RateControlMode.Crf
                )
            );
    }

    public void Dispose()
    {
        foreach (string directory in _tempDirectories)
        {
            if (Directory.Exists(path: directory))
                Directory.Delete(path: directory, recursive: true);
        }
    }

    // Success-path tests must look like a finished encode on disk: the
    // finalize stage refuses to write a master playlist when no variant
    // produced measurable segments.
    private string CreateSeededOutputDirectory()
    {
        string outputDirectory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"nomercy-encoder-tests-{Guid.NewGuid():N}"
        );
        _tempDirectories.Add(item: outputDirectory);

        SeedVariant(outputDirectory: outputDirectory, name: "video_1920x1080_SDR");
        SeedVariant(outputDirectory: outputDirectory, name: "audio_en_aac");

        return outputDirectory;
    }

    private static void SeedVariant(string outputDirectory, string name)
    {
        string variantDirectory = Path.Combine(path1: outputDirectory, path2: name);
        Directory.CreateDirectory(path: variantDirectory);
        File.WriteAllBytes(path: Path.Combine(path1: variantDirectory, path2: $"{name}_00000.ts"), bytes: new byte[120_000]);
        File.WriteAllText(
            path: Path.Combine(path1: variantDirectory, path2: $"{name}.m3u8"),
            contents: $"#EXTM3U\n#EXTINF:6.000000,\n{name}_00000.ts\n#EXT-X-ENDLIST\n"
        );
    }

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(hours: 2),
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
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams:
            [
                new(
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

    private static EncodingProfile BuildProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Test",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: V2RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.1",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        );

    private void SetupSuccessPath(string inputPath = "/movies/test.mkv")
    {
        MediaInfo media = BuildMediaInfo();

        _storage.Setup(expression: s => s.Exists(inputPath)).Returns(value: true);
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(inputPath, It.IsAny<IStorage>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: media);
        _ffmpegExecutor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromMinutes(minutes: 10),
                    Error: null
                )
            );
    }

    // ------------------------------------------------------------------
    // Full pipeline success
    // ------------------------------------------------------------------

    [Fact]
    public async Task FullPipeline_AllStagesSucceed_ReturnsSuccess()
    {
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.OutputPath.Should().Be(expected: outputDirectory);
    }

    [Fact]
    public async Task FullPipeline_DurationIsPositive()
    {
        SetupSuccessPath();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);

        result.Duration.Should().BeGreaterThan(expected: TimeSpan.Zero);
    }

    [Fact]
    public async Task FullPipeline_MetricsHaveEncoderName()
    {
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);

        result.Metrics.Should().NotBeNull();
        result.Metrics!.EncoderUsed.Should().Be(expected: "libx264");
    }

    // ------------------------------------------------------------------
    // Analyze failure stops pipeline
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeFailure_FileMissing_ReturnsFalseWithError()
    {
        _storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: false);

        EncodingRequest request = new(
            InputPath: "/missing/file.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(expected: EncodingErrorKind.InputNotFound);
    }

    [Fact]
    public async Task AnalyzeFailure_DoesNotCallExecutor()
    {
        _storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: false);

        EncodingRequest request = new(
            InputPath: "/missing/file.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request: request);

        _ffmpegExecutor.Verify(
            expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    // ------------------------------------------------------------------
    // Execute failure stops pipeline
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteFailure_FfmpegCrashes_ReturnsFalseWithError()
    {
        MediaInfo media = BuildMediaInfo();

        _storage.Setup(expression: s => s.Exists("/movies/test.mkv")).Returns(value: true);
        _analyzer
            .Setup(expression: a =>
                a.AnalyzeAsync(
                    "/movies/test.mkv",
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: media);
        _ffmpegExecutor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: false,
                    ExitCode: 1,
                    StdErr: "encoder error: resource exhausted",
                    Duration: TimeSpan.FromSeconds(seconds: 5),
                    Error: new(
                        Kind: EncodingErrorKind.ResourceExhausted,
                        Message: "Resource exhausted",
                        FfmpegStderr: "encoder error: resource exhausted",
                        StageName: "Execute",
                        Recoverable: true
                    )
                )
            );

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(expected: EncodingErrorKind.ResourceExhausted);
    }

    // ------------------------------------------------------------------
    // Progress observer is called
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProgressObserver_OnCompleted_CalledOnSuccess()
    {
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        Mock<IProgressObserver> progressMock = new();
        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request: request, progress: progressMock.Object);

        progressMock.Verify(
            expression: p => p.OnStageCompleted(It.IsAny<string>(), It.IsAny<TimeSpan>()),
            times: Times.AtLeast(callCount: 6)
        );
    }

    [Fact]
    public async Task ProgressObserver_OnError_CalledOnFailure()
    {
        _storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: false);

        Mock<IProgressObserver> progressMock = new();
        EncodingRequest request = new(
            InputPath: "/missing.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request: request, progress: progressMock.Object);

        progressMock.Verify(expression: p => p.OnError(It.IsAny<EncodingError>()), times: Times.Once);
    }

    // ------------------------------------------------------------------
    // Reconstruction metadata wiring (EncodingRequest.MediaItem)
    //
    // The production wiring in VideoEncodeJob only ever sets MediaItem on the
    // coordinator's FinalizeOnly EncodingRequest (see HandleFinalizeAsync) —
    // never on a request that reaches BuildStage. These tests prove that
    // boundary is exactly what protects every existing user's ffmpeg command.
    // ------------------------------------------------------------------

    private static MovieMediaRef MovieRef() =>
        new(
            Type: MediaType.Movie,
            Id: 550,
            Title: "Fight Club",
            Year: 1999,
            Description: "A movie."
        );

    private static EpisodeMediaRef EpisodeRef() =>
        new(
            Type: MediaType.Episode,
            Id: 62085,
            Title: "Pilot",
            Year: 2008,
            ShowTitle: "Breaking Bad",
            SeasonNumber: 1,
            EpisodeNumber: 1,
            Description: "An episode."
        );

    [Fact]
    public async Task CriterionA_MediaItemOnFinalizeOnlyRequest_NeverReachesBuildOrExecute()
    {
        // Criterion A regression guard: FinalizeOnly=true skips Build+Execute
        // entirely (see Encoder.EncodeAsync). This is the ONLY reason it is
        // safe to populate MediaItem on this request — there is no ffmpeg
        // command for the value to ever influence.
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile(),
            Options: new(FinalizeOnly: true),
            MediaItem: MovieRef()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);

        result.Success.Should().BeTrue();
        _ffmpegExecutor.Verify(
            expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never,
            failMessage: "FinalizeOnly must skip Build+Execute — this is what makes MediaItem safe to populate"
        );
    }

    [Fact]
    public async Task CriterionB_MediaItemOnBuildExecutingRequest_InjectionDefaultOff_CommandIsIdentical()
    {
        // Criterion B: MediaItem is now attached to every production encode
        // request (including ones where Build/Execute run — the Whole-task
        // inline path) so it can drive manifest/reconstruction writes. This
        // pins the guarantee that makes that safe: with
        // EncodingOptions.EnableMetadataInjection left at its default (false —
        // what VideoEncodeJob sets today), the emitted ffmpeg command is
        // byte-for-byte identical whether or not MediaItem is populated.
        SetupSuccessPath();

        List<string[]> capturedArgs = [];
        _ffmpegExecutor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(action: (cmd, _, _, _, _) => capturedArgs.Add(item: cmd.Arguments))
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromMinutes(minutes: 10),
                    Error: null
                )
            );

        string plainDirectory = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            request: new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: plainDirectory,
                Profile: BuildProfile()
            )
        );
        string[] plainArgs = capturedArgs[index: 0];
        capturedArgs.Clear();

        string withItemDirectory = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            request: new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: withItemDirectory,
                Profile: BuildProfile(),
                MediaItem: MovieRef()
            )
        );
        string[] withItemArgs = capturedArgs[index: 0];

        plainArgs.Should().NotContain(unexpected: "-metadata");
        withItemArgs.Should().NotContain(unexpected: "-metadata");
        withItemArgs
            .Should()
            .Equal(
                expected: plainArgs,
                because: "MediaItem is pure identity — with EnableMetadataInjection left at its "
                         + "default, populating it must never change a single argv token"
            );
    }

    [Fact]
    public async Task CriterionB_EnableMetadataInjectionExplicitlyOn_CommandContainsMetadataFlags()
    {
        // The opt-in still works end-to-end when a caller explicitly asks for
        // it — keeps the original MetadataInjectorBuildStageIntegrationTests
        // coverage meaningful under the new explicit contract.
        SetupSuccessPath();

        List<string[]> capturedArgs = [];
        _ffmpegExecutor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(action: (cmd, _, _, _, _) => capturedArgs.Add(item: cmd.Arguments))
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromMinutes(minutes: 10),
                    Error: null
                )
            );

        string outputDirectory = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            request: new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: outputDirectory,
                Profile: BuildProfile(),
                Options: new(EnableMetadataInjection: true),
                MediaItem: MovieRef()
            )
        );
        string[] args = capturedArgs[index: 0];

        args.Should().Contain(expected: "-metadata");
        ContainsPair(args: args, flag: "-metadata", value: "title=Fight Club").Should().BeTrue();
    }

    [Fact]
    public async Task CriterionA_InlineWholeTaskShapedRequest_WritesBlueprint()
    {
        // Criterion A: the exact request shape VideoEncodeJob.RunInlineAsync
        // sends to Encoder.EncodeAsync (Options left null — Build+Execute+
        // Finalize all run, no FinalizeOnly) must still produce the
        // .nomercy.json blueprint once MediaItem is attached, not only the
        // coordinator's separate FinalizeOnly pass.
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile(),
            MediaTitle: "Fight Club.NoMercy",
            MediaItem: MovieRef()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);
        result.Success.Should().BeTrue();

        // At the media folder root — never nested under encodes/{slug}/.
        string blueprintPath = Path.Combine(path1: outputDirectory, path2: MediaBlueprintWriter.FileName);

        File.Exists(path: blueprintPath)
            .Should()
            .BeTrue(because: "the inline Whole-task path must write .nomercy.json");

        MediaBlueprint? blueprint = JsonConvert.DeserializeObject<MediaBlueprint>(
            value: File.ReadAllText(path: blueprintPath)
        );
        blueprint.Should().NotBeNull();
        blueprint!.Identity.Type.Should().Be(expected: "movie");
        blueprint.Identity.TmdbId.Should().Be(expected: 550);
        blueprint.Source.Path.Should().Be(expected: "/movies/test.mkv");
        blueprint.Encodes.Should().ContainSingle();
        blueprint.Encodes[index: 0].Tracks.Should().NotBeEmpty();
        blueprint.Encodes[index: 0].ReconstructionCommandTemplate.Should().NotBeNullOrWhiteSpace();
    }

    private static bool ContainsPair(string[] args, string flag, string value)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag && args[i + 1] == value)
                return true;
        }
        return false;
    }

    [Fact]
    public async Task CriterionB_RealOutputFileNames_AreIdentical_WithAndWithoutMediaItem()
    {
        // The real per-encode file set (video_*/audio_*/master m3u8) must be
        // byte-for-byte the same set of relative paths whether or not
        // MediaItem (and therefore BundleLayout) is resolved — attaching a
        // MediaItem only adds the .nomercy.json blueprint at the media root,
        // it never renames or relocates anything BuildStage already writes.
        SetupSuccessPath();

        // Explicit, identical MediaTitle on both requests — otherwise the
        // master playlist filename would embed each run's random temp
        // directory name and never compare equal across two separate runs.
        string withoutItemDir = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            request: new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: withoutItemDir,
                Profile: BuildProfile(),
                MediaTitle: "Fight Club.NoMercy"
            )
        );

        string withItemDir = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            request: new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: withItemDir,
                Profile: BuildProfile(),
                MediaTitle: "Fight Club.NoMercy",
                Options: new(FinalizeOnly: true),
                MediaItem: MovieRef()
            )
        );

        static IEnumerable<string> RealFiles(string root) =>
            Directory
                .EnumerateFiles(path: root, searchPattern: "*", searchOption: SearchOption.AllDirectories)
                .Select(selector: f => Path.GetRelativePath(relativeTo: root, path: f).Replace(oldChar: '\\', newChar: '/'))
                .Where(predicate: rel =>
                    !rel.Equals(value: MediaBlueprintWriter.FileName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(keySelector: rel => rel, comparer: StringComparer.Ordinal);

        RealFiles(root: withoutItemDir).Should().BeEquivalentTo(expectation: RealFiles(root: withItemDir));

        // The new sidecar lands ONLY at the media folder root — never nested
        // under a preset-scoped sub-directory.
        File.Exists(path: Path.Combine(path1: withItemDir, path2: MediaBlueprintWriter.FileName)).Should().BeTrue();
    }

    [Fact]
    public async Task CriterionC_MovieEncode_WritesBlueprint_WithMeaningfulContent()
    {
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile(),
            Options: new(FinalizeOnly: true),
            MediaItem: MovieRef()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);
        result.Success.Should().BeTrue();

        // At the media folder root — never nested under encodes/{slug}/.
        string blueprintPath = Path.Combine(path1: outputDirectory, path2: MediaBlueprintWriter.FileName);
        File.Exists(path: blueprintPath)
            .Should()
            .BeTrue(because: ".nomercy.json must be written for a movie encode");

        MediaBlueprint? blueprint = JsonConvert.DeserializeObject<MediaBlueprint>(
            value: File.ReadAllText(path: blueprintPath)
        );
        blueprint.Should().NotBeNull();
        blueprint!.Identity.Type.Should().Be(expected: "movie");
        blueprint.Identity.TmdbId.Should().Be(expected: 550);
        blueprint.Source.Path.Should().Be(expected: "/movies/test.mkv");
        blueprint.Source.Container.Should().Be(expected: "matroska");

        BlueprintEncode encode = blueprint.Encodes.Should().ContainSingle().Which;
        encode.PresetSlug.Should().Be(expected: "test");
        encode.Tracks.Should().NotBeEmpty();
        encode.ReconstructionCommandTemplate.Should().NotBeNullOrWhiteSpace();
        // BuildProfile()'s video rung transcodes h264 -> libx264 (CRF) — never
        // a stream copy — so it is lossy and must surface a warning explaining
        // what is not losslessly recoverable.
        encode.LossyWarnings.Should().NotBeEmpty();
        encode.LossyWarnings.Should().Contain(predicate: w => w.Contains("video"));
        // Source audio is already AAC and the profile also targets AAC — the
        // planner smart-copies instead of re-encoding, so this track is
        // genuinely lossless. Asserting on the track itself (not just "some
        // warning exists") proves the fidelity classification is accurate,
        // not just present.
        encode
            .Tracks.Should()
            .Contain(predicate: t => t.Kind == "audio" && t.Fidelity == "lossless" && t.Policy == "copy");
    }

    [Fact]
    public async Task CriterionC_EpisodeEncode_WritesBlueprint()
    {
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile(),
            Options: new(FinalizeOnly: true),
            MediaItem: EpisodeRef()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);
        result.Success.Should().BeTrue();

        string blueprintPath = Path.Combine(path1: outputDirectory, path2: MediaBlueprintWriter.FileName);
        File.Exists(path: blueprintPath)
            .Should()
            .BeTrue(because: ".nomercy.json must be written for an episode encode");

        MediaBlueprint? blueprint = JsonConvert.DeserializeObject<MediaBlueprint>(
            value: File.ReadAllText(path: blueprintPath)
        );
        blueprint.Should().NotBeNull();
        blueprint!.Identity.Type.Should().Be(expected: "episode");
        blueprint.Identity.TmdbId.Should().Be(expected: 62085);
    }

    [Fact]
    public async Task NoResolvableMediaItem_StillEncodesFine_WithoutManifestOrReconstruction()
    {
        // Disc-rip / non-library source: no movie or episode to attach.
        // Degrades exactly like today — the encode must still succeed.
        SetupSuccessPath();
        string outputDirectory = CreateSeededOutputDirectory();

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: outputDirectory,
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request: request);

        result.Success.Should().BeTrue();
        Directory.Exists(path: Path.Combine(path1: outputDirectory, path2: "encodes")).Should().BeFalse();
    }
}
