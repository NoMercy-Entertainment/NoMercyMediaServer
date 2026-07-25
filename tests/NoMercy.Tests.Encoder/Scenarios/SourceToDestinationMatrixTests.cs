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
using LadderConfig = NoMercy.Encoder.Profiles.LadderConfig;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using LadderRung = NoMercy.Encoder.Profiles.LadderRung;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Comprehensive matrix of source→destination scenario tests ensuring the
/// planner never makes copy-vs-transcode, upscale, or container-compatibility
/// mistakes. Each test drives PlanStage.ExecuteAsync and asserts on the
/// resulting OutputPlan.
/// </summary>
public class SourceToDestinationMatrixTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public SourceToDestinationMatrixTests()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

        SetupCodecResolver();

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

    private void SetupCodecResolver()
    {
        _codecResolver
            .Setup(r =>
                r.Resolve(
                    VideoCodecType.Copy,
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildResolvedCodec("copy", true));

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    VideoCodecType.H265,
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildResolvedCodec("libx265", true));

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    VideoCodecType.H264,
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildResolvedCodec("libx264", false));

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    VideoCodecType.Av1,
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(BuildResolvedCodec("libaom-av1", true));
    }

    private static ResolvedCodec BuildResolvedCodec(string ffmpegName, bool supports10Bit) =>
        new(
            ffmpegName,
            new(
                ffmpegName,
                null,
                ffmpegName == "copy" ? [] : ["medium"],
                ffmpegName == "copy" ? [] : ["main", "main10"],
                ffmpegName == "copy" ? [] : ["5.1"],
                new(0, 51, 23),
                [RateControlMode.Crf],
                supports10Bit,
                supports10Bit,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            null,
            RateControlMode.Crf
        );

    private static MediaInfo BuildMedia(
        int width,
        int height,
        string videoCodec = "hevc",
        int bitDepth = 8,
        int bitRateKbps = 6000,
        string audioCodec = "aac",
        int audioChannels = 2,
        int audioBitRateKbps = 128,
        bool includeAudio = true
    ) =>
        new(
            "/media/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            bitRateKbps + (includeAudio ? audioBitRateKbps : 0),
            14_400_000_000,
            [
                new(
                    0,
                    videoCodec,
                    width,
                    height,
                    24.0,
                    bitDepth,
                    bitDepth >= 10 ? "yuv420p10le" : "yuv420p",
                    "bt709",
                    "bt709",
                    "bt709",
                    true,
                    bitRateKbps
                ),
            ],
            includeAudio
                ?
                [
                    new(
                        0,
                        audioCodec,
                        audioChannels,
                        48000,
                        audioBitRateKbps,
                        "eng",
                        true,
                        false
                    ),
                ]
                : [],
            [],
            []
        );

    private static NoMercy.Encoder.Profiles.VideoOutput BuildVideoOutput(
        VideoCodecType codec,
        int width = 1920,
        int height = 1080,
        int bitDepth = 8,
        StreamPolicy policy = StreamPolicy.Transcode
    ) =>
        new(
            policy,
            codec,
            width,
            height,
            V2RateControlMode.Crf,
            23,
            6000,
            null,
            null,
            "medium",
            CodecProfile.Auto,
            null,
            null,
            bitDepth,
            null,
            2,
            false,
            "video/{label}",
            "video/{label}/playlist"
        );

    private static NoMercy.Encoder.Profiles.AudioOutput BuildAudioOutput(
        AudioCodecType codec,
        int bitRateKbps = 128,
        int channels = 2,
        StreamPolicy policy = StreamPolicy.Transcode
    ) =>
        new(
            policy,
            codec,
            bitRateKbps,
            channels,
            48000,
            [],
            null,
            null,
            null,
            "audio/{lang}/{codec}",
            "audio/{lang}/{codec}/playlist"
        );

    private static EncodingProfile BuildProfile(
        NoMercy.Encoder.Profiles.VideoOutput? video = null,
        NoMercy.Encoder.Profiles.AudioOutput[]? audio = null,
        Container container = Container.HlsFmp4
    ) =>
        new(
            Ulid.NewUlid(),
            "TestProfile",
            container,
            video,
            audio ?? [],
            []
        );

    private async Task<OutputPlan> RunPlanStage(MediaInfo media, EncodingProfile profile)
    {
        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    // VIDEO SCENARIOS

    [Fact]
    public async Task Video_MatchingResolutionAndCodecAndBitrate_CopiesStream()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("copy");
        video.Height.Should().Be(1080);
    }

    [Fact]
    public async Task Video_DifferentCodec_H264ToHevc_Transcodes()
    {
        MediaInfo media = BuildMedia(1920, 1080, "h264", 8, 4000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libx265");
    }

    [Fact]
    public async Task Video_SourceUpscale_1080pSourceTo2160pProfile_ClampsToSourceHeight()
    {
        MediaInfo media = BuildMedia(3840, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 3840, 2160, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video
            .Height.Should()
            .BeLessThanOrEqualTo(1080, "profile height must not exceed source height");
        video.EncoderName.Should().Be("libx265");
    }

    [Fact]
    public async Task Video_10BitSourceTo8BitProfile_Transcodes()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 10, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libx265", "bit depth mismatch forces transcode");
    }

    [Fact]
    public async Task Video_8BitSourceTo10BitProfile_Transcodes()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 10)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libx265", "bit depth mismatch forces transcode");
    }

    [Fact]
    public async Task Video_HevcSourceInHlsTs_Transcodes()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8),
            container: Container.HlsTs
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libx265", "HlsTs only carries H.264");
    }

    [Fact]
    public async Task Video_H264SourceInHlsTs_Copies()
    {
        // Source bitrate must be >= the profile target for smart-copy (spec
        // §video passthrough); 20 Mbps clears any default so codec+res+depth
        // match is the only remaining gate → copy.
        MediaInfo media = BuildMedia(1920, 1080, "h264", 8, 20000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H264, 1920, 1080, 8),
            container: Container.HlsTs
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("copy");
    }

    [Fact]
    public async Task Video_SourceBitrateBelowTarget_Transcodes()
    {
        // Smart-copy needs source bitrate >= target. A source BELOW the target
        // is insufficient → transcode up to meet it.
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 2000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8) with
            {
                BitrateKbps = 3000,
            }
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libx265", "source bitrate below target requires transcode");
    }

    [Fact]
    public async Task Video_SourceBitrateAboveTarget_Copies()
    {
        // Documented contract: a source richer than the target is COPYABLE —
        // the target bitrate is a transcode ceiling, not a mandate to shrink a
        // good source (that would only lose quality).
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8) with
            {
                BitrateKbps = 3000,
            }
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("copy", "a source above the target bitrate is copyable");
    }

    // LADDER SCENARIOS

    [Fact]
    public async Task Ladder_SourceHeightRungCopies_HigherRungsTranscode()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 10, 6000);
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            Name: "LadderTest",
            Container: Container.HlsFmp4,
            Video: BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 10),
            Audio: [],
            Subtitles: [],
            Ladder: new LadderConfig
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new LadderRung(
                        1920,
                        1080,
                        VideoCodecType.H265,
                        6000,
                        9000,
                        12000,
                        24.0,
                        "medium",
                        CodecProfile.Auto,
                        10,
                        "yuv420p10le"
                    ),
                    new LadderRung(
                        1280,
                        720,
                        VideoCodecType.H265,
                        3000,
                        4500,
                        6000,
                        24.0,
                        "medium",
                        CodecProfile.Auto,
                        10,
                        "yuv420p10le"
                    ),
                ],
            }
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        plan.VideoOutputs.Should().HaveCount(2);
        plan.VideoOutputs[0].Height.Should().Be(1080);
        plan.VideoOutputs[0].EncoderName.Should().Be("copy");
        plan.VideoOutputs[1].Height.Should().Be(720);
        plan.VideoOutputs[1].EncoderName.Should().Be("libx265");
    }

    [Fact]
    public async Task Ladder_AllRungsAboveSourceHeight_NoRungsAdded()
    {
        MediaInfo media = BuildMedia(1920, 720, "hevc", 8, 3000);
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            Name: "LadderAboveSource",
            Container: Container.HlsFmp4,
            Video: BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8),
            Audio: [],
            Subtitles: [],
            Ladder: new LadderConfig
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new LadderRung(
                        3840,
                        2160,
                        VideoCodecType.H265,
                        15000,
                        0,
                        0,
                        24.0,
                        "medium",
                        CodecProfile.Auto,
                        8,
                        null
                    ),
                    new LadderRung(
                        1920,
                        1080,
                        VideoCodecType.H265,
                        6000,
                        0,
                        0,
                        24.0,
                        "medium",
                        CodecProfile.Auto,
                        8,
                        null
                    ),
                ],
            }
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        plan.VideoOutputs.Should().NotBeEmpty();
        plan.VideoOutputs.All(v => v.Height <= 720)
            .Should()
            .BeTrue("no rung should upscale beyond source height");
    }

    // AUDIO SCENARIOS

    [Fact]
    public async Task Audio_MatchingCodecAndBitrateAndChannels_Copies()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            audioCodec: "aac",
            audioChannels: 2,
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            null,
            [BuildAudioOutput(AudioCodecType.Aac, 192, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        AudioOutputPlan audioOutput = Assert.Single(plan.AudioOutputs);
        audioOutput.Action.Should().Be(StreamAction.Copy);
        audioOutput.EncoderName.Should().Be("copy");
    }

    [Fact]
    public async Task Audio_DifferentCodec_Transcodes()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            audioCodec: "aac",
            audioChannels: 2,
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            null,
            [BuildAudioOutput(AudioCodecType.Opus, 128, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        AudioOutputPlan audioOutput = Assert.Single(plan.AudioOutputs);
        audioOutput.Action.Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public async Task Audio_SourceBitrateBelowTarget_Transcodes()
    {
        // Source bitrate below the target is insufficient for smart-copy
        // (spec §35.1: copy requires source bitrate >= profile bitrate).
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            audioCodec: "aac",
            audioChannels: 2,
            audioBitRateKbps: 96
        );
        EncodingProfile profile = BuildProfile(
            null,
            [BuildAudioOutput(AudioCodecType.Aac, 128, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        AudioOutputPlan audioOutput = Assert.Single(plan.AudioOutputs);
        audioOutput.Action.Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public async Task Audio_SourceFewerChannelsThanTarget_Transcodes()
    {
        // Copy needs source channels >= target (spec §35.1). A mono source
        // cannot satisfy a stereo target → transcode (up-mix). Bitrate is kept
        // sufficient so the channel count is the only gate.
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            audioCodec: "aac",
            audioChannels: 1,
            audioBitRateKbps: 384
        );
        EncodingProfile profile = BuildProfile(
            null,
            [BuildAudioOutput(AudioCodecType.Aac, 128, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        AudioOutputPlan audioOutput = Assert.Single(plan.AudioOutputs);
        audioOutput.Action.Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public async Task Audio_SufficientBitrateAndChannels_Copies()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            audioCodec: "aac",
            audioChannels: 2,
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            null,
            [BuildAudioOutput(AudioCodecType.Aac, 128, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        AudioOutputPlan audioOutput = Assert.Single(plan.AudioOutputs);
        audioOutput.Action.Should().Be(StreamAction.Copy);
    }

    [Fact]
    public async Task Audio_OpusInHlsTs_Transcodes()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            audioCodec: "opus",
            audioChannels: 2,
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H264, 1920, 1080, 8),
            [BuildAudioOutput(AudioCodecType.Opus, 192, 2)],
            Container.HlsTs
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        AudioOutputPlan audioOutput = Assert.Single(plan.AudioOutputs);
        audioOutput.Action.Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public async Task Audio_AacInHlsTs_Copies()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            audioCodec: "aac",
            audioChannels: 2,
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H264, 1920, 1080, 8),
            [BuildAudioOutput(AudioCodecType.Aac, 128, 2)],
            Container.HlsTs
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        AudioOutputPlan audioOutput = Assert.Single(plan.AudioOutputs);
        audioOutput.Action.Should().Be(StreamAction.Copy);
    }

    // COMBINED VIDEO + AUDIO SCENARIOS

    [Fact]
    public async Task VideoAndAudio_BothCopyable_BothCopy()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            videoCodec: "hevc",
            bitDepth: 8,
            bitRateKbps: 6000,
            audioCodec: "aac",
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8),
            [BuildAudioOutput(AudioCodecType.Aac, 128, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        video.EncoderName.Should().Be("copy");
        audio.Action.Should().Be(StreamAction.Copy);
    }

    [Fact]
    public async Task VideoAndAudio_VideoCopyAudioTranscode_OnlyAudioTranscodes()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            videoCodec: "hevc",
            bitDepth: 8,
            bitRateKbps: 6000,
            audioCodec: "aac",
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8),
            [BuildAudioOutput(AudioCodecType.Opus, 128, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        video.EncoderName.Should().Be("copy");
        audio.Action.Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public async Task VideoAndAudio_VideoTranscodeAudioCopy_OnlyVideoTranscodes()
    {
        MediaInfo media = BuildMedia(
            1920,
            height: 1080,
            videoCodec: "h264",
            bitDepth: 8,
            bitRateKbps: 4000,
            audioCodec: "aac",
            audioBitRateKbps: 192
        );
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8),
            [BuildAudioOutput(AudioCodecType.Aac, 128, 2)]
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        AudioOutputPlan audio = Assert.Single(plan.AudioOutputs);
        video.EncoderName.Should().Be("libx265");
        audio.Action.Should().Be(StreamAction.Copy);
    }

    // CODEC MISMATCH SCENARIOS

    [Fact]
    public async Task Video_H265SourceToAv1Profile_Transcodes()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.Av1, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libaom-av1");
    }

    [Fact]
    public async Task Video_H264ToH265_Transcodes()
    {
        MediaInfo media = BuildMedia(1920, 1080, "h264", 8, 4000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libx265");
    }

    // RESOLUTION EDGE CASES

    [Fact]
    public async Task Video_ExactHeightMatch_Copies()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("copy");
        video.Height.Should().Be(1080);
    }

    [Fact]
    public async Task Video_SourceHeightAboveProfile_Transcodes()
    {
        MediaInfo media = BuildMedia(1920, 1440, "hevc", 8, 9000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("libx265");
    }

    [Fact]
    public async Task Video_ProfileHeightNull_KeepsSourceHeight()
    {
        MediaInfo media = BuildMedia(1920, 1080, "hevc", 8, 6000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8) with
            {
                Height = null,
            }
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.EncoderName.Should().Be("copy");
    }

    [Fact]
    public async Task Video_720pSourceTo1080p_OutputNotUpscaled()
    {
        MediaInfo media = BuildMedia(1280, 720, "hevc", 8, 3000);
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(VideoCodecType.H265, 1920, 1080, 8)
        );

        OutputPlan plan = await RunPlanStage(media, profile);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.Height.Should().BeLessThanOrEqualTo(720, "output must never upscale beyond source");
    }
}
