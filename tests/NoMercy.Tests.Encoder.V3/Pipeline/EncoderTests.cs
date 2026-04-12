namespace NoMercy.Tests.Encoder.V3.Pipeline;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Composition;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Execution;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Infrastructure;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Pipeline.Optimizer;
using NoMercy.Encoder.V3.Pipeline.Stages;
using NoMercy.Encoder.V3.Profiles;
using NoMercy.Encoder.V3.Progress;

public class EncoderTests
{
    private readonly Mock<IMediaAnalyzer> _analyzer = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IProfileValidator> _profileValidator = new();
    private readonly Mock<IFfmpegExecutor> _ffmpegExecutor = new();
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();

    private readonly Encoder _encoder;

    public EncoderTests()
    {
        SetupDefaultHardware();
        SetupDefaultCodecResolver();

        EncoderOptions options = new("ffmpeg", "ffprobe");

        AnalyzeStage analyzeStage = new(
            _analyzer.Object,
            _fileSystem.Object,
            NullLogger<AnalyzeStage>.Instance
        );
        ValidateStage validateStage = new(
            _profileValidator.Object,
            NullLogger<ValidateStage>.Instance
        );
        PlanStage planStage = new(
            new ExecutionGraphBuilder(),
            new GroupingStrategy(),
            new CostEstimator(),
            _codecResolver.Object,
            _hardware.Object,
            NullLogger<PlanStage>.Instance
        );
        BuildStage buildStage = new(options, NullLogger<BuildStage>.Instance);
        ExecuteStage executeStage = new(_ffmpegExecutor.Object, NullLogger<ExecuteStage>.Instance);
        FinalizeStage finalizeStage = new(NullLogger<FinalizeStage>.Instance);

        _encoder = new Encoder(
            analyzeStage,
            validateStage,
            planStage,
            buildStage,
            executeStage,
            finalizeStage,
            NullLogger<Encoder>.Instance
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
                    EncoderInfo: new EncoderInfo(
                        FfmpegName: "libx264",
                        RequiredVendor: null,
                        Presets: ["medium"],
                        Profiles: ["high"],
                        Levels: ["4.1"],
                        QualityRange: new QualityRange(0, 51, 23),
                        SupportedRateControl: [RateControlMode.Crf],
                        Supports10Bit: false,
                        SupportsHdr: false,
                        MaxConcurrentSessions: int.MaxValue,
                        PixelFormat10Bit: "yuv420p10le",
                        VendorSpecificFlags: new Dictionary<string, string>()
                    ),
                    Device: null,
                    DefaultRateControl: RateControlMode.Crf
                )
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
                new VideoStreamInfo(
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
                new AudioStreamInfo(
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
            Id: "test-id",
            Name: "Test",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new VideoOutput(
                    Codec: VideoCodecType.H264,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 4000,
                    Crf: 23,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.1",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new AudioOutput(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"]
                ),
            ],
            SubtitleOutputs: []
        );

    private void SetupSuccessPath(string inputPath = "/movies/test.mkv")
    {
        MediaInfo media = BuildMediaInfo();

        _fileSystem.Setup(fs => fs.FileExists(inputPath)).Returns(true);
        _analyzer
            .Setup(a => a.AnalyzeAsync(inputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        _profileValidator
            .Setup(v => v.Validate(It.IsAny<EncodingProfile>()))
            .Returns(ValidationResult.Success());
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

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: "/output/test",
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.OutputPath.Should().Be("/output/test");
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

        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: "/output/test",
            Profile: BuildProfile()
        );

        EncodingResult result = await _encoder.EncodeAsync(request);

        result.Metrics.EncoderUsed.Should().Be("libx264");
    }

    // ------------------------------------------------------------------
    // Analyze failure stops pipeline
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeFailure_FileMissing_ReturnsFalseWithError()
    {
        _fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

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
        _fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

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

        _fileSystem.Setup(fs => fs.FileExists("/movies/test.mkv")).Returns(true);
        _analyzer
            .Setup(a => a.AnalyzeAsync("/movies/test.mkv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);
        _profileValidator
            .Setup(v => v.Validate(It.IsAny<EncodingProfile>()))
            .Returns(ValidationResult.Success());
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
                    Error: new EncodingError(
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

        Mock<IProgressObserver> progressMock = new();
        EncodingRequest request = new(
            InputPath: "/movies/test.mkv",
            OutputDirectory: "/output/test",
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request, progressMock.Object);

        progressMock.Verify(p => p.OnCompleted(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProgressObserver_OnError_CalledOnFailure()
    {
        _fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        Mock<IProgressObserver> progressMock = new();
        EncodingRequest request = new(
            InputPath: "/missing.mkv",
            OutputDirectory: "/output/test",
            Profile: BuildProfile()
        );

        await _encoder.EncodeAsync(request, progressMock.Object);

        progressMock.Verify(p => p.OnError(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
