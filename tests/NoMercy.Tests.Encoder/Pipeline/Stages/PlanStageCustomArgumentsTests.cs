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
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// CustomArguments is the encoder's customization escape hatch — the only way a
/// profile or an IEncoderPlugin can pass ffmpeg flags the schema doesn't model.
/// These pin that it actually reaches the output, not just that it validates.
/// </summary>
public class PlanStageCustomArgumentsTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegCapabilities = new();
    private readonly PlanStage _stage;

    public PlanStageCustomArgumentsTests()
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
            _ffmpegCapabilities.Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task VideoCustomArguments_ReachTheOutputExtraFlags()
    {
        EncodingProfile profile = BuildProfile(
            customArgs: new() { ["-x264-params"] = "keyint=48:min-keyint=48" }
        );

        OutputPlan plan = await RunPlan(profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video
            .ExtraFlags.Should()
            .ContainKey("-x264-params", "video CustomArguments must reach the encode")
            .WhoseValue.Should()
            .Be("keyint=48:min-keyint=48");
    }

    private async Task<OutputPlan> RunPlan(EncodingProfile profile)
    {
        ValidateInput input = new(BuildSdrMedia(), profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildSdrMedia() =>
        new(
            FilePath: "/media/sdr.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 4_000_000_000,
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
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile BuildProfile(Dictionary<string, string> customArgs) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "CustomArgs Test",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: V2RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 5000,
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
                SegmentNameTemplate: "video/{label}",
                PlaylistNameTemplate: "video/{label}/playlist",
                CustomArguments: customArgs
            ),
            Audio: [],
            Subtitles: []
        );
}
