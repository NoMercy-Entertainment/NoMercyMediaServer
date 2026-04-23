namespace NoMercy.Tests.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Audio;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

public class PlanStageDownmixTests
{
    private readonly PlanStage _stage;

    public PlanStageDownmixTests()
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

        _stage = new(
            new(),
            new(),
            new(),
            codecResolver.Object,
            hardware.Object,
            new TonemapSelector(),
            new Mock<IFfmpegCapabilities>().Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BuildAudioFilter — unit-level
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildAudioFilter_DownmixAuto_Unchanged()
    {
        string? filter = PlanStage.BuildAudioFilter(LoudnessMode.None, DownmixMode.Auto, null);

        filter.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_StereoItuR128_EmitsPanWithBs775Coefficients()
    {
        string? filter = PlanStage.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.StereoItuR128,
            null
        );

        filter
            .Should()
            .Be("pan=stereo|FL<FL+0.707*FC+0.707*BL+0.707*SL|FR<FR+0.707*FC+0.707*BR+0.707*SR");
    }

    [Fact]
    public void BuildAudioFilter_Mono_EmitsPanMonoMatrix()
    {
        string? filter = PlanStage.BuildAudioFilter(LoudnessMode.None, DownmixMode.Mono, null);

        filter.Should().StartWith("pan=mono|c0<");
        filter.Should().Contain("FL");
        filter.Should().Contain("FR");
    }

    [Fact]
    public void BuildAudioFilter_CustomMatrix_WrapsPanPrefix()
    {
        string? filter = PlanStage.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Custom,
            "stereo|FL<1.0*FL|FR<1.0*FR"
        );

        filter.Should().Be("pan=stereo|FL<1.0*FL|FR<1.0*FR");
    }

    [Fact]
    public void BuildAudioFilter_CustomWithoutMatrix_ReturnsNull()
    {
        string? filter = PlanStage.BuildAudioFilter(LoudnessMode.None, DownmixMode.Custom, "   ");

        filter.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_PanAndLoudnorm_ChainsPanBeforeLoudnorm()
    {
        // Pan filter must run before loudnorm so the normalizer sees the final
        // channel layout — otherwise loudnorm measures the multichannel signal
        // and under-normalizes the downmix.
        string? filter = PlanStage.BuildAudioFilter(
            LoudnessMode.EbuR128,
            DownmixMode.StereoItuR128,
            null
        );

        int panIdx = filter!.IndexOf("pan=", StringComparison.Ordinal);
        int loudIdx = filter.IndexOf("loudnorm=", StringComparison.Ordinal);

        panIdx.Should().BeGreaterThanOrEqualTo(0);
        loudIdx.Should().BeGreaterThan(panIdx);
        filter.Should().Contain(",");
    }

    [Fact]
    public void BuildAudioFilter_LoudnormOnly_NoPanPrefix()
    {
        string? filter = PlanStage.BuildAudioFilter(LoudnessMode.EbuR128, DownmixMode.Auto, null);

        filter.Should().Be("loudnorm=I=-16:TP=-1.5:LRA=11");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // End-to-end through the plan stage
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlanStage_StereoDownmixRequested_PlanAudioFilterContainsPan()
    {
        EncodingProfile profile = BuildProfile(DownmixMode.StereoItuR128, LoudnessMode.None);
        OutputPlan plan = await RunPlan(profile);

        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        audio.AudioFilter.Should().StartWith("pan=stereo");
    }

    [Fact]
    public async Task PlanStage_StereoDownmixWithLoudnorm_ChainsBoth()
    {
        EncodingProfile profile = BuildProfile(DownmixMode.StereoItuR128, LoudnessMode.EbuR128);
        OutputPlan plan = await RunPlan(profile);

        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        audio.AudioFilter.Should().Contain("pan=stereo");
        audio.AudioFilter.Should().Contain("loudnorm=");
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

    private static EncodingProfile BuildProfile(DownmixMode downmix, LoudnessMode loudness) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Downmix Test",
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
                    AllowedLanguages: [],
                    Loudness: loudness,
                    Downmix: downmix
                ),
            ],
            SubtitleOutputs: []
        );
}
