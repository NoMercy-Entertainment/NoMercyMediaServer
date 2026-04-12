namespace NoMercy.Tests.Encoder.V3.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Pipeline.Optimizer;
using NoMercy.Encoder.V3.Pipeline.Stages;
using NoMercy.Encoder.V3.Profiles;

public class PlanStageTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly ExecutionGraphBuilder _graphBuilder = new();
    private readonly GroupingStrategy _groupingStrategy = new();
    private readonly CostEstimator _costEstimator = new();
    private readonly PlanStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public PlanStageTests()
    {
        // Default hardware: no GPU
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

        // Default codec resolver — return software H264 encoder
        _codecResolver
            .Setup(r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildSoftwareH264Codec());

        _stage = new PlanStage(
            _graphBuilder,
            _groupingStrategy,
            _costEstimator,
            _codecResolver.Object,
            _hardware.Object,
            NullLogger<PlanStage>.Instance
        );
    }

    private static ResolvedCodec BuildSoftwareH264Codec() =>
        new(
            FfmpegEncoderName: "libx264",
            EncoderInfo: new EncoderInfo(
                FfmpegName: "libx264",
                RequiredVendor: null,
                Presets: ["slow", "medium", "fast"],
                Profiles: ["high"],
                Levels: ["4.1"],
                QualityRange: new QualityRange(0, 51, 23),
                SupportedRateControl: [RateControlMode.Crf, RateControlMode.Cbr],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            Device: null,
            DefaultRateControl: RateControlMode.Crf
        );

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

    private static EncodingProfile BuildSimpleProfile() =>
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

    // ------------------------------------------------------------------
    // Simple profile → execution plan with at least one group
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_ProducesExecutionPlanWithGroups()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.Groups.Should().NotBeEmpty();
    }

    // ------------------------------------------------------------------
    // Plan has a non-zero estimated duration
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_EstimatedDurationIsPositive()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.EstimatedTotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // ------------------------------------------------------------------
    // Plan has an OutputPlan with matching format
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_OutputPlanMatchesFormat()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.Should().NotBeNull();
        plan.OutputPlan.Format.Should().Be(OutputFormat.Hls);
    }

    // ------------------------------------------------------------------
    // Plan has video + audio outputs
    // ------------------------------------------------------------------

    [Fact]
    public async Task SimpleProfile_OutputPlanContainsVideoAndAudio()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = BuildSimpleProfile();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs.Should().HaveCount(1);
        plan.OutputPlan.VideoOutputs[0].EncoderName.Should().Be("libx264");
        plan.OutputPlan.AudioOutputs.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------
    // Multi-output profile → multiple video output plans
    // ------------------------------------------------------------------

    [Fact]
    public async Task MultiOutputProfile_ProducesMultipleVideoOutputPlans()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Id: "multi",
            Name: "Multi",
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
                new VideoOutput(
                    Codec: VideoCodecType.H264,
                    Width: 1280,
                    Height: 720,
                    BitrateKbps: 2500,
                    Crf: 25,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
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

        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ExecutionPlan>>();
        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs.Should().HaveCount(2);
    }
}
