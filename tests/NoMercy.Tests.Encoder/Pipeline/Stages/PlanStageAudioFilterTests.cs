namespace NoMercy.Tests.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

public class PlanStageAudioFilterTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;

    public PlanStageAudioFilterTests()
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

        _stage = new PlanStage(
            new ExecutionGraphBuilder(),
            new GroupingStrategy(),
            new CostEstimator(),
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task LoudnessNone_NoAudioFilter()
    {
        EncodingProfile profile = BuildProfile(LoudnessMode.None);
        OutputPlan plan = await RunPlan(profile);

        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        Assert.Null(audio.AudioFilter);
    }

    [Fact]
    public async Task LoudnessEbuR128_EmitsLoudnormWithR128Targets()
    {
        EncodingProfile profile = BuildProfile(LoudnessMode.EbuR128);
        OutputPlan plan = await RunPlan(profile);

        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        Assert.Equal("loudnorm=I=-16:TP=-1.5:LRA=11", audio.AudioFilter);
    }

    [Fact]
    public async Task LoudnessReplayGain_EmitsLoudnormWithRgTargets()
    {
        EncodingProfile profile = BuildProfile(LoudnessMode.ReplayGain);
        OutputPlan plan = await RunPlan(profile);

        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        Assert.Equal("loudnorm=I=-18:TP=-1.5:LRA=11", audio.AudioFilter);
    }

    [Fact]
    public async Task LoudnessCustom_NoAutoFilter()
    {
        // Custom mode means the profile's CustomArguments carry the filter — the mapper
        // does not emit one automatically.
        EncodingProfile profile = BuildProfile(LoudnessMode.Custom);
        OutputPlan plan = await RunPlan(profile);

        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        Assert.Null(audio.AudioFilter);
    }

    private async Task<OutputPlan> RunPlan(EncodingProfile profile)
    {
        ValidateInput input = new(BuildMedia(), profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);

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
                    Codec: "ac3",
                    Channels: 6,
                    SampleRate: 48000,
                    BitRateKbps: 640,
                    Language: "en",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

    private static EncodingProfile BuildProfile(LoudnessMode loudness) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Audio Filter Test",
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
                    AllowedLanguages: [],
                    Loudness: loudness
                ),
            ],
            SubtitleOutputs: []
        );
}
