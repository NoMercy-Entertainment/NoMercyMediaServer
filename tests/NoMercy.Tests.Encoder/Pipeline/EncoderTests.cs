using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Jobs;
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
            NullLogger<PlanStage>.Instance
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
            TestStorageFactory.CreateLocal()
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
            TestStorageFactory.CreateLocal()
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
            OutputDirectory: "/output/test",
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
            OutputDirectory: "/output/test",
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
            OutputDirectory: "/output/test",
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
            OutputDirectory: "/output/test",
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
            OutputDirectory: "/output/test",
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request, progressMock.Object);

        progressMock.Verify(p => p.OnError(It.IsAny<EncodingError>()), Times.Once);
    }
}
