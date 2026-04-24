namespace NoMercy.Tests.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

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

        _stage = new(
            _graphBuilder,
            _groupingStrategy,
            _costEstimator,
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    private static ResolvedCodec BuildSoftwareH264Codec() =>
        new(
            FfmpegEncoderName: "libx264",
            EncoderInfo: new(
                FfmpegName: "libx264",
                RequiredVendor: null,
                Presets: ["slow", "medium", "fast"],
                Profiles: ["high"],
                Levels: ["4.1"],
                QualityRange: new(0, 51, 23),
                SupportedRateControl: [RateControlMode.Crf, RateControlMode.Cbr],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
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

    private static EncodingProfile BuildSimpleProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Test",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
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
                new(
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

    // ------------------------------------------------------------------
    // 10-bit downgrade guard — encoders that don't support 10-bit must
    // fall back to 8-bit instead of emitting an empty pixel format.
    // Regression: previously "v.TenBit ? encoder.PixelFormat10Bit : yuv420p"
    // would emit "" when PixelFormat10Bit was empty, and ffmpeg would
    // either pick the source format or fail with "Invalid pixel format".
    // ------------------------------------------------------------------

    [Fact]
    public async Task TenBitRequested_EncoderLacks10Bit_DowngradedTo8Bit()
    {
        // Default resolver returns libx264 with Supports10Bit=false + empty PixelFormat10Bit.
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "TenBitDowngrade",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
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
                    TenBit: true
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"]
                ),
            ],
            SubtitleOutputs: []
        );

        StageResult result = await _stage.ExecuteAsync(new(media, profile), _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs[0].TenBit.Should().BeFalse("encoder doesn't support 10-bit");
        plan.OutputPlan.VideoOutputs[0].PixelFormat.Should().Be("yuv420p");
    }

    [Fact]
    public async Task TenBitRequested_EncoderSupports10Bit_KeepsTenBit()
    {
        // Override resolver to return an encoder that DOES support 10-bit.
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
                    FfmpegEncoderName: "libx265",
                    EncoderInfo: new(
                        FfmpegName: "libx265",
                        RequiredVendor: null,
                        Presets: ["slow", "medium", "fast"],
                        Profiles: ["main", "main10"],
                        Levels: ["4.1"],
                        QualityRange: new(0, 51, 28),
                        SupportedRateControl: [RateControlMode.Crf],
                        Supports10Bit: true,
                        SupportsHdr: true,
                        MaxConcurrentSessions: int.MaxValue,
                        PixelFormat10Bit: "yuv420p10le",
                        VendorSpecificFlags: new()
                    ),
                    Device: null,
                    DefaultRateControl: RateControlMode.Crf
                )
            );

        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "TenBit",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H265,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 4000,
                    Crf: 28,
                    Preset: "medium",
                    Profile: "main10",
                    Level: "4.1",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: true
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"]
                ),
            ],
            SubtitleOutputs: []
        );

        StageResult result = await _stage.ExecuteAsync(new(media, profile), _context, default);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        plan.OutputPlan.VideoOutputs[0].TenBit.Should().BeTrue();
        plan.OutputPlan.VideoOutputs[0].PixelFormat.Should().Be("yuv420p10le");
    }

    [Fact]
    public async Task MultiOutputProfile_ProducesMultipleVideoOutputPlans()
    {
        MediaInfo media = BuildMediaInfo();
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Multi",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
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
                new(
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
                new(
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
