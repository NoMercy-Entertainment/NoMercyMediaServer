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
using DownmixConfig = NoMercy.Encoder.Profiles.DownmixConfig;
using DownmixMode = NoMercy.Encoder.Profiles.DownmixMode;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LoudnessConfig = NoMercy.Encoder.Profiles.LoudnessConfig;
using LoudnessMode = NoMercy.Encoder.Profiles.LoudnessMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class PlanStageAudioFilterTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;

    public PlanStageAudioFilterTests()
    {
        _hardware.Setup(expression: h => h.HasGpu).Returns(value: false);
        _hardware.Setup(expression: h => h.CpuCores).Returns(value: 8);
        _hardware.Setup(expression: h => h.Gpus).Returns(value: []);
        _hardware.Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(value: false);
        _hardware
            .Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns(value: (GpuDevice?)null);

        _codecResolver
            .Setup(expression: r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                value: new ResolvedCodec(
                    FfmpegEncoderName: "libx264",
                    EncoderInfo: new(
                        FfmpegName: "libx264",
                        RequiredVendor: null,
                        Presets: ["medium"],
                        Profiles: ["high"],
                        Levels: ["4.1"],
                        QualityRange: new(Min: 0, Max: 51, Default: 23),
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
            graphBuilder: new(),
            groupingStrategy: new(),
            costEstimator: new(),
            codecResolver: _codecResolver.Object,
            hardware: _hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: new Mock<IFfmpegCapabilities>().Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task LoudnessNone_NoAudioFilter()
    {
        EncodingProfile profile = BuildProfile(loudness: LoudnessMode.None);
        OutputPlan plan = await RunPlan(profile: profile);

        AudioOutputPlan audio = Assert.Single(collection: plan.AudioOutputs);
        Assert.Null(@object: audio.AudioFilter);
    }

    [Fact]
    public async Task LoudnessEbuR128_EmitsLoudnormWithR128Targets()
    {
        EncodingProfile profile = BuildProfile(loudness: LoudnessMode.EbuR128);
        OutputPlan plan = await RunPlan(profile: profile);

        AudioOutputPlan audio = Assert.Single(collection: plan.AudioOutputs);
        Assert.Equal(expected: "loudnorm=I=-16:TP=-1.5:LRA=11", actual: audio.AudioFilter);
    }

    [Fact]
    public async Task LoudnessReplayGain_EmitsLoudnormWithRgTargets()
    {
        EncodingProfile profile = BuildProfile(loudness: LoudnessMode.ReplayGain);
        OutputPlan plan = await RunPlan(profile: profile);

        AudioOutputPlan audio = Assert.Single(collection: plan.AudioOutputs);
        Assert.Equal(expected: "loudnorm=I=-18:TP=-1.5:LRA=11", actual: audio.AudioFilter);
    }

    [Fact]
    public async Task LoudnessCustom_NoAutoFilter()
    {
        // Custom mode means the profile's CustomArguments carry the filter — the mapper
        // does not emit one automatically.
        EncodingProfile profile = BuildProfile(loudness: LoudnessMode.Custom);
        OutputPlan plan = await RunPlan(profile: profile);

        AudioOutputPlan audio = Assert.Single(collection: plan.AudioOutputs);
        Assert.Null(@object: audio.AudioFilter);
    }

    [Fact]
    public async Task DownmixAndLoudness_ChainsPanBeforeLoudnorm()
    {
        // loudnorm expects the post-downmix channel layout, so pan must run
        // first. The two filters chain as "pan=...,loudnorm=..." in that order.
        EncodingProfile profile = BuildProfile(
            loudness: LoudnessMode.EbuR128,
            downmix: new(Mode: DownmixMode.StereoItuR128)
        );
        OutputPlan plan = await RunPlan(profile: profile);

        AudioOutputPlan audio = Assert.Single(collection: plan.AudioOutputs);
        Assert.Equal(
            expected: "pan=stereo|FL<FL+0.707*FC+0.707*BL+0.707*SL|FR<FR+0.707*FC+0.707*BR+0.707*SR,"
                      + "loudnorm=I=-16:TP=-1.5:LRA=11",
            actual: audio.AudioFilter
        );
    }

    [Fact]
    public async Task DownmixOnly_EmitsPanWithoutLoudnorm()
    {
        EncodingProfile profile = BuildProfile(
            loudness: LoudnessMode.None,
            downmix: new(Mode: DownmixMode.Mono)
        );
        OutputPlan plan = await RunPlan(profile: profile);

        AudioOutputPlan audio = Assert.Single(collection: plan.AudioOutputs);
        audio.AudioFilter.Should().StartWith(expected: "pan=mono|");
        audio.AudioFilter.Should().NotContain(unexpected: "loudnorm");
    }

    private async Task<OutputPlan> RunPlan(EncodingProfile profile)
    {
        ValidateInput input = new(Media: BuildMedia(), Profile: profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildMedia() =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
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

    private static EncodingProfile BuildProfile(
        LoudnessMode loudness,
        DownmixConfig? downmix = null
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Audio Filter Test",
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
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: loudness == LoudnessMode.None ? null : new LoudnessConfig(Mode: loudness),
                    Downmix: downmix,
                    SegmentNameTemplate: "audio/{lang}-{codec}",
                    PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: []
        );
}
