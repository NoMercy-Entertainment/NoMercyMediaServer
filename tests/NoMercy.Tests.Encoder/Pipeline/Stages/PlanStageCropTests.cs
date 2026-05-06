using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.V2.CodecProfile;
using Container = NoMercy.Encoder.Profiles.V2.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.V2.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.V2.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.V2.RateControlMode;
using VideoOutput = NoMercy.Encoder.Profiles.V2.VideoOutput;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class PlanStageCropTests
{
    [Fact]
    public async Task AutoDetectCropOff_DetectorNotInvoked()
    {
        Mock<ICropDetector> detector = new();
        PlanStage stage = BuildStage(detector.Object);

        EncodingProfile profile = BuildProfile(autoDetectCrop: false);
        OutputPlan plan = await RunPlan(stage, profile);

        detector.Verify(
            d => d.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_ShouldCrop_PopulatesCropFilterOnAllOutputs()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(d => d.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CropResult(Width: 1920, Height: 800, X: 0, Y: 140, ShouldCrop: true));

        PlanStage stage = BuildStage(detector.Object);
        EncodingProfile profile = BuildProfile(autoDetectCrop: true);

        OutputPlan plan = await RunPlan(stage, profile);

        plan.VideoOutputs.Should().HaveCountGreaterThan(0);
        plan.VideoOutputs[0].CropFilter.Should().Be("1920:800:0:140");
        detector.Verify(
            d => d.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task AutoDetectCropOn_NoCropNeeded_CropFilterNull()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(d => d.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CropResult(0, 0, 0, 0, ShouldCrop: false));

        PlanStage stage = BuildStage(detector.Object);
        EncodingProfile profile = BuildProfile(autoDetectCrop: true);

        OutputPlan plan = await RunPlan(stage, profile);

        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    [Fact]
    public async Task AutoDetectCropOn_DetectorThrows_ContinuesWithoutCrop()
    {
        Mock<ICropDetector> detector = new();
        detector
            .Setup(d => d.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        PlanStage stage = BuildStage(detector.Object);
        EncodingProfile profile = BuildProfile(autoDetectCrop: true);

        OutputPlan plan = await RunPlan(stage, profile);

        plan.VideoOutputs[0].CropFilter.Should().BeNull();
    }

    private static PlanStage BuildStage(ICropDetector cropDetector)
    {
        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(h => h.HasGpu).Returns(false);
        hardware.Setup(h => h.CpuCores).Returns(8);
        hardware.Setup(h => h.Gpus).Returns([]);
        hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        hardware.Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>())).Returns((GpuDevice?)null);

        Mock<ICodecResolver> codecResolver = new();
        codecResolver
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

        return new(
            new(),
            new(),
            new(),
            codecResolver.Object,
            hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            cropDetector,
            NullLogger<PlanStage>.Instance
        );
    }

    private static async Task<OutputPlan> RunPlan(PlanStage stage, EncodingProfile profile)
    {
        ValidateInput input = new(BuildMedia(), profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await stage.ExecuteAsync(input, context, CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMedia() =>
        new(
            FilePath: "/media/test.mkv",
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
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile BuildProfile(bool autoDetectCrop) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Crop Test",
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
                SegmentNameTemplate: "video/{label}",
                PlaylistNameTemplate: "video/{label}/playlist"
            ),
            Audio: [],
            Subtitles: [],
            AutoDetectCrop: autoDetectCrop
        );
}
