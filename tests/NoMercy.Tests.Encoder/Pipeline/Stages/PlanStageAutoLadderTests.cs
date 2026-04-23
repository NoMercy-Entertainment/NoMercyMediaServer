namespace NoMercy.Tests.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

public class PlanStageAutoLadderTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;

    public PlanStageAutoLadderTests()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

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

        _stage = new(
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
    }

    [Fact]
    public async Task AutoLadder_Off_PreservesManualVariants()
    {
        EncodingProfile profile = BuildProfile(
            autoLadder: false,
            videoOutputs: [BuildVideo(1920, 1080), BuildVideo(1280, 720)]
        );

        OutputPlan plan = await RunPlan(BuildMedia(1920, 1080), profile);

        Assert.Equal(2, plan.VideoOutputs.Length);
        Assert.Contains(plan.VideoOutputs, v => v.Height == 1080);
        Assert.Contains(plan.VideoOutputs, v => v.Height == 720);
    }

    [Fact]
    public async Task AutoLadder_On_Expand1080pReference_Produces360_480_720_1080()
    {
        EncodingProfile profile = BuildProfile(
            autoLadder: true,
            videoOutputs: [BuildVideo(1920, 1080)]
        );

        OutputPlan plan = await RunPlan(BuildMedia(1920, 1080, bitrateKbps: 6000), profile);

        int[] heights = plan.VideoOutputs.Select(v => v.Height).ToArray();
        Assert.Contains(360, heights);
        Assert.Contains(480, heights);
        Assert.Contains(720, heights);
        Assert.Contains(1080, heights);
    }

    [Fact]
    public async Task AutoLadder_On_720pSource_SkipsHigherTiers()
    {
        EncodingProfile profile = BuildProfile(
            autoLadder: true,
            videoOutputs: [BuildVideo(1280, 720)]
        );

        OutputPlan plan = await RunPlan(BuildMedia(1280, 720, bitrateKbps: 3000), profile);

        Assert.All(plan.VideoOutputs, v => Assert.True(v.Height <= 720));
        Assert.DoesNotContain(1080, plan.VideoOutputs.Select(v => v.Height));
    }

    [Fact]
    public async Task AutoLadder_On_MultipleVariants_FallsBackToManual()
    {
        // AutoLadder requires exactly 1 reference profile — with more than 1,
        // the stage logs a warning and keeps the manual variants.
        EncodingProfile profile = BuildProfile(
            autoLadder: true,
            videoOutputs: [BuildVideo(1920, 1080), BuildVideo(1280, 720)]
        );

        OutputPlan plan = await RunPlan(BuildMedia(1920, 1080), profile);

        Assert.Equal(2, plan.VideoOutputs.Length);
    }

    [Fact]
    public async Task AutoLadder_On_AudioOnlySource_NoExpansion()
    {
        EncodingProfile profile = BuildProfile(
            autoLadder: true,
            videoOutputs: [BuildVideo(1920, 1080)]
        );

        // Source has no video streams → auto-ladder passthrough.
        OutputPlan plan = await RunPlan(BuildAudioOnlyMedia(), profile);

        Assert.Empty(plan.VideoOutputs);
    }

    private async Task<OutputPlan> RunPlan(MediaInfo media, EncodingProfile profile)
    {
        ValidateInput input = new(media, profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMedia(int width, int height, long bitrateKbps = 6000) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: bitrateKbps + 500,
            FileSizeBytes: 4_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: bitrateKbps
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static MediaInfo BuildAudioOnlyMedia() =>
        new(
            FilePath: "/media/song.flac",
            Format: "flac",
            Duration: TimeSpan.FromMinutes(4),
            OverallBitRateKbps: 800,
            FileSizeBytes: 20_000_000,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile BuildProfile(bool autoLadder, VideoOutput[] videoOutputs) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Test",
            Format: OutputFormat.Hls,
            VideoOutputs: videoOutputs,
            AudioOutputs: [],
            SubtitleOutputs: [],
            AutoLadder: autoLadder
        );

    private static VideoOutput BuildVideo(int width, int height) =>
        new(
            Codec: VideoCodecType.H264,
            Width: width,
            Height: height,
            BitrateKbps: 4000,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
}
