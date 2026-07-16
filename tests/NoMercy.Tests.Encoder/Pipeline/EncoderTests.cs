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
            _analyzer.Object,
            _storage.Object,
            NullLogger<AnalyzeStage>.Instance
        );
        ValidateStage validateStage = new(NullLogger<ValidateStage>.Instance);
        PlanStage planStage = new(
            new(),
            new(),
            new(),
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance,
            outputNamingResolver: new OutputNamingResolver(new MediaKeyResolver())
        );
        OutputStrategyFactory outputFactory = new([
            new HlsOutputStrategy(TestStorageFactory.CreateLocal()),
            new MkvOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp4OutputStrategy(TestStorageFactory.CreateLocal()),
            new DashOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp3OutputStrategy(TestStorageFactory.CreateLocal()),
            new FlacOutputStrategy(TestStorageFactory.CreateLocal()),
            new OggOutputStrategy(TestStorageFactory.CreateLocal()),
        ]);
        BuildStage buildStage = new(
            options,
            new FontExtractor(TestStorageFactory.CreateLocal()),
            new SubtitleExtractor(),
            outputFactory,
            [],
            NullLogger<BuildStage>.Instance,
            TestStorageFactory.CreateLocal(),
            metadataInjector: new MetadataInjector(),
            metadataMerger: new MetadataMerger()
        );
        ExecuteStage executeStage = new(
            _ffmpegExecutor.Object,
            new Mock<ICheckpointStore>().Object,
            NullLogger<ExecuteStage>.Instance
        );
        FinalizeStage finalizeStage = new(
            new ChapterWriter(TestStorageFactory.CreateLocal()),
            new FontExtractor(TestStorageFactory.CreateLocal()),
            outputFactory,
            NullLogger<FinalizeStage>.Instance,
            TestStorageFactory.CreateLocal(),
            manifestWriter: new BundleManifestWriter(),
            reconstructionWriter: new ReconstructionWriter()
        );

        _encoder = new(
            analyzeStage,
            validateStage,
            planStage,
            buildStage,
            executeStage,
            finalizeStage,
            NullLogger<NoMercy.Encoder.Pipeline.Encoder>.Instance
        );
    }

    private void SetupDefaultHardware()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);
    }

    private void SetupDefaultCodecResolver()
    {
        _codecResolver
            .Setup(r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                new ResolvedCodec(
                    FfmpegEncoderName: "libx264",
                    EncoderInfo: new(
                        FfmpegName: "libx264",
                        RequiredVendor: null,
                        Presets: ["medium"],
                        Profiles: ["high"],
                        Levels: ["4.1"],
                        QualityRange: new(0, 51, 23),
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
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    // Success-path tests must look like a finished encode on disk: the
    // finalize stage refuses to write a master playlist when no variant
    // produced measurable segments.
    private string CreateSeededOutputDirectory()
    {
        string outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"nomercy-encoder-tests-{Guid.NewGuid():N}"
        );
        _tempDirectories.Add(outputDirectory);

        SeedVariant(outputDirectory, "video_1920x1080_SDR");
        SeedVariant(outputDirectory, "audio_en_aac");

        return outputDirectory;
    }

    private static void SeedVariant(string outputDirectory, string name)
    {
        string variantDirectory = Path.Combine(outputDirectory, name);
        Directory.CreateDirectory(variantDirectory);
        File.WriteAllBytes(Path.Combine(variantDirectory, $"{name}_00000.ts"), new byte[120_000]);
        File.WriteAllText(
            Path.Combine(variantDirectory, $"{name}.m3u8"),
            $"#EXTM3U\n#EXTINF:6.000000,\n{name}_00000.ts\n#EXT-X-ENDLIST\n"
        );
    }

    private static MediaInfo BuildMediaInfo() =>
        new(
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
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

        _storage.Setup(s => s.Exists(inputPath)).Returns(true);
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(inputPath, It.IsAny<IStorage>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(media);
        _ffmpegExecutor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromMinutes(10),
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

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.OutputPath.Should().Be(outputDirectory);
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

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
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

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Metrics.Should().NotBeNull();
        result.Metrics!.EncoderUsed.Should().Be("libx264");
    }

    // ------------------------------------------------------------------
    // Analyze failure stops pipeline
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeFailure_FileMissing_ReturnsFalseWithError()
    {
        _storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        EncodingRequest request = new(
            InputPath: "/missing/file.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(EncodingErrorKind.InputNotFound);
    }

    [Fact]
    public async Task AnalyzeFailure_DoesNotCallExecutor()
    {
        _storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        EncodingRequest request = new(
            InputPath: "/missing/file.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request);

        _ffmpegExecutor.Verify(
            e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ------------------------------------------------------------------
    // Execute failure stops pipeline
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteFailure_FfmpegCrashes_ReturnsFalseWithError()
    {
        MediaInfo media = BuildMediaInfo();

        _storage.Setup(s => s.Exists("/movies/test.mkv")).Returns(true);
        _analyzer
            .Setup(a =>
                a.AnalyzeAsync(
                    "/movies/test.mkv",
                    It.IsAny<IStorage>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(media);
        _ffmpegExecutor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ExecutionResult(
                    Success: false,
                    ExitCode: 1,
                    StdErr: "encoder error: resource exhausted",
                    Duration: TimeSpan.FromSeconds(5),
                    Error: new(
                        EncodingErrorKind.ResourceExhausted,
                        "Resource exhausted",
                        "encoder error: resource exhausted",
                        "Execute",
                        true
                    )
                )
            );

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(EncodingErrorKind.ResourceExhausted);
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

        await _encoder.EncodeAsync(request, progressMock.Object);

        progressMock.Verify(
            p => p.OnStageCompleted(It.IsAny<string>(), It.IsAny<TimeSpan>()),
            Times.AtLeast(6)
        );
    }

    [Fact]
    public async Task ProgressObserver_OnError_CalledOnFailure()
    {
        _storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        Mock<IProgressObserver> progressMock = new();
        EncodingRequest request = new(
            InputPath: "/missing.mkv",
            OutputDirectory: "/tmp/nmtest-output/test",
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request, progressMock.Object);

        progressMock.Verify(p => p.OnError(It.IsAny<EncodingError>()), Times.Once);
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

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Success.Should().BeTrue();
        _ffmpegExecutor.Verify(
            e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "FinalizeOnly must skip Build+Execute — this is what makes MediaItem safe to populate"
        );
    }

    [Fact]
    public async Task Guardrail_MediaItemOnABuildExecutingRequest_ChangesTheCommand()
    {
        // Pins the DANGER this feature must never trigger in production:
        // BuildStage injects -metadata whenever MediaItem is non-null,
        // regardless of FinalizeOnly. VideoEncodeJob must never set MediaItem
        // on a request that reaches Build — if this test starts failing, that
        // invariant has been violated somewhere in the wiring.
        SetupSuccessPath();

        List<string[]> capturedArgs = [];
        _ffmpegExecutor
            .Setup(e =>
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
            >((cmd, _, _, _, _) => capturedArgs.Add(cmd.Arguments))
            .ReturnsAsync(
                new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromMinutes(10),
                    Error: null
                )
            );

        string plainDirectory = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: plainDirectory,
                Profile: BuildProfile()
            )
        );
        string[] plainArgs = capturedArgs[0];
        capturedArgs.Clear();

        string withItemDirectory = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: withItemDirectory,
                Profile: BuildProfile(),
                MediaItem: MovieRef()
            )
        );
        string[] withItemArgs = capturedArgs[0];

        plainArgs.Should().NotContain("-metadata");
        withItemArgs.Should().Contain("-metadata");
        ContainsPair(withItemArgs, "-metadata", "title=Fight Club").Should().BeTrue();
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
        // MediaItem (and therefore BundleLayout) is resolved — the layout only
        // adds NEW sidecar files under encodes/{slug}/, it never renames or
        // relocates anything BuildStage already writes.
        SetupSuccessPath();

        // Explicit, identical MediaTitle on both requests — otherwise the
        // master playlist filename would embed each run's random temp
        // directory name and never compare equal across two separate runs.
        string withoutItemDir = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            new EncodingRequest(
                InputPath: "/movies/test.mkv",
                OutputDirectory: withoutItemDir,
                Profile: BuildProfile(),
                MediaTitle: "Fight Club.NoMercy"
            )
        );

        string withItemDir = CreateSeededOutputDirectory();
        await _encoder.EncodeAsync(
            new EncodingRequest(
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
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Where(rel => !rel.StartsWith("encodes/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(rel => rel, StringComparer.Ordinal);

        RealFiles(withoutItemDir).Should().BeEquivalentTo(RealFiles(withItemDir));

        // The new sidecars land ONLY under encodes/{slug}/ — never at the root
        // next to the real media files.
        Directory
            .EnumerateFiles(withItemDir, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(withItemDir, f).Replace('\\', '/'))
            .Should()
            .OnlyContain(rel => rel.StartsWith("encodes/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CriterionC_MovieEncode_WritesManifestAndReconstruction_WithMeaningfulContent()
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

        EncodingResult result = await _encoder.EncodeAsync(request);
        result.Success.Should().BeTrue();

        string bundleDir = Path.Combine(outputDirectory, "encodes", "test");
        string manifestPath = Path.Combine(bundleDir, "manifest.json");
        string reconstructionPath = Path.Combine(bundleDir, "reconstruction.json");

        File.Exists(manifestPath)
            .Should()
            .BeTrue("manifest.json must be written for a movie encode");
        File.Exists(reconstructionPath)
            .Should()
            .BeTrue("reconstruction.json must be written for a movie encode");

        BundleManifest? manifest = JsonConvert.DeserializeObject<BundleManifest>(
            File.ReadAllText(manifestPath)
        );
        manifest.Should().NotBeNull();
        manifest!.MediaType.Should().Be("movie");
        manifest.MediaId.Should().Be(550);
        manifest.PresetSlug.Should().Be("test");
        manifest.Files.Should().NotBeEmpty();

        Reconstruction? reconstruction = JsonConvert.DeserializeObject<Reconstruction>(
            File.ReadAllText(reconstructionPath)
        );
        reconstruction.Should().NotBeNull();
        reconstruction!.Source.OriginalPath.Should().Be("/movies/test.mkv");
        reconstruction.Source.Container.Should().Be("matroska");
        reconstruction.Tracks.Should().NotBeEmpty();
        reconstruction.CommandTemplate.Should().NotBeNullOrWhiteSpace();
        // BuildProfile()'s video rung transcodes h264 -> libx264 (CRF) — never
        // a stream copy — so it is lossy and must surface a warning explaining
        // what is not losslessly recoverable.
        reconstruction.LossyWarnings.Should().NotBeEmpty();
        reconstruction.LossyWarnings.Should().Contain(w => w.Contains("video"));
        // Source audio is already AAC and the profile also targets AAC — the
        // planner smart-copies instead of re-encoding, so this track is
        // genuinely lossless. Asserting on the track itself (not just "some
        // warning exists") proves the fidelity classification is accurate,
        // not just present.
        reconstruction
            .Tracks.Should()
            .Contain(t => t.Kind == "audio" && t.Fidelity == "lossless" && t.Policy == "copy");
    }

    [Fact]
    public async Task CriterionC_EpisodeEncode_WritesManifestAndReconstruction()
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

        EncodingResult result = await _encoder.EncodeAsync(request);
        result.Success.Should().BeTrue();

        string manifestPath = Path.Combine(outputDirectory, "encodes", "test", "manifest.json");
        File.Exists(manifestPath)
            .Should()
            .BeTrue("manifest.json must be written for an episode encode");

        BundleManifest? manifest = JsonConvert.DeserializeObject<BundleManifest>(
            File.ReadAllText(manifestPath)
        );
        manifest.Should().NotBeNull();
        manifest!.MediaType.Should().Be("episode");
        manifest.MediaId.Should().Be(62085);
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

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Success.Should().BeTrue();
        Directory.Exists(Path.Combine(outputDirectory, "encodes")).Should().BeFalse();
    }
}
