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
                    "libx264",
                    new(
                        "libx264",
                        null,
                        ["medium"],
                        ["high"],
                        ["4.1"],
                        new(0, 51, 23),
                        [RateControlMode.Crf],
                        false,
                        false,
                        int.MaxValue,
                        "yuv420p10le",
                        new()
                    ),
                    null,
                    RateControlMode.Crf
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
        string? filter = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Auto,
            null
        );

        filter.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_StereoItuR128_EmitsPanWithBs775Coefficients()
    {
        string? filter = AudioFilterBuilder.BuildAudioFilter(
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
        string? filter = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Mono,
            null
        );

        filter.Should().StartWith("pan=mono|c0<");
        filter.Should().Contain("FL");
        filter.Should().Contain("FR");
    }

    [Fact]
    public void BuildAudioFilter_CustomMatrix_WrapsPanPrefix()
    {
        string? filter = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Custom,
            "stereo|FL<1.0*FL|FR<1.0*FR"
        );

        filter.Should().Be("pan=stereo|FL<1.0*FL|FR<1.0*FR");
    }

    [Fact]
    public void BuildAudioFilter_CustomWithoutMatrix_ReturnsNull()
    {
        string? filter = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Custom,
            "   "
        );

        filter.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_PanAndLoudnorm_ChainsPanBeforeLoudnorm()
    {
        // Pan filter must run before loudnorm so the normalizer sees the final
        // channel layout — otherwise loudnorm measures the multichannel signal
        // and under-normalizes the downmix.
        string? filter = AudioFilterBuilder.BuildAudioFilter(
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
        string? filter = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.EbuR128,
            DownmixMode.Auto,
            null
        );

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
            "/media/test.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            8000,
            4_000_000_000,
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    6000
                ),
            ],
            [
                new(
                    1,
                    "ac3",
                    6,
                    48000,
                    640,
                    "en",
                    true,
                    false
                ),
            ],
            [],
            []
        );

    private static EncodingProfile BuildProfile(DownmixMode downmix, LoudnessMode loudness) =>
        new(
            Ulid.NewUlid(),
            "Downmix Test",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                V2RateControlMode.Crf,
                23,
                4000,
                null,
                null,
                "medium",
                CodecProfile.High,
                "4.1",
                null,
                8,
                null,
                2,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    [],
                    null,
                    loudness == LoudnessMode.None ? null : new LoudnessConfig(loudness),
                    downmix == DownmixMode.Auto ? null : new DownmixConfig(downmix),
                    "audio/{lang}-{codec}",
                    "audio/{lang}-{codec}/playlist"
                ),
            ],
            []
        );
}
