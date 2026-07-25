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
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// End-to-end coverage for the audio smart-copy downgrade wired into
/// <see cref="PlanStage"/> — the audio equivalent of
/// <see cref="PlanStageSmartCopyTests"/>. A source stream that already
/// satisfies a Transcode <see cref="AudioOutput"/> losslessly should route
/// through Copy (<c>-c:a copy</c>) instead of re-encoding.
/// </summary>
public class PlanStageAudioSmartCopyTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public PlanStageAudioSmartCopyTests()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);

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

    private static MediaInfo BuildAudioOnlyMedia(
        string codec,
        int channels = 2,
        int sampleRate = 48000,
        long bitRateKbps = 192,
        string language = "eng"
    ) =>
        new(
            "/music/test.mkv",
            "matroska",
            TimeSpan.FromMinutes(3),
            bitRateKbps,
            5_000_000,
            [],
            [
                new(
                    0,
                    codec,
                    channels,
                    sampleRate,
                    bitRateKbps,
                    language,
                    true,
                    false
                ),
            ],
            [],
            []
        );

    private static AudioOutput BuildAudioOutput(
        AudioCodecType codec,
        int bitrateKbps = 128,
        int channels = 2,
        int sampleRateHz = 48000
    ) =>
        new(
            StreamPolicy.Transcode,
            codec,
            bitrateKbps,
            channels,
            sampleRateHz,
            [],
            null,
            null,
            null,
            "audio_{lang}_{codec}/audio_{lang}_{codec}",
            "audio_{lang}_{codec}/audio_{lang}_{codec}"
        );

    private static EncodingProfile BuildProfile(
        AudioOutput[] audio,
        Container container = Container.HlsFmp4
    ) =>
        new(
            Ulid.NewUlid(),
            "AudioSmartCopy",
            container,
            null,
            audio,
            []
        );

    [Fact]
    public async Task AacSourceMatchingAacOutput_DowngradesToCopy()
    {
        MediaInfo media = BuildAudioOnlyMedia("aac", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile([BuildAudioOutput(AudioCodecType.Aac)]);

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        AudioOutputPlan audio = Assert.Single(success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(StreamAction.Copy);
        audio.EncoderName.Should().Be("copy");
        audio
            .CodecToken.Should()
            .Be(
                "aac",
                "the on-disk rendition must name the real source codec, not the literal \"copy\" pseudo-encoder"
            );

        _context
            .Decisions!.Snapshot()
            .Should()
            .Contain(entry => entry.Key == "plan.audio_smart_copy");
    }

    [Fact]
    public async Task OpusSourceAgainstAacRequestedOutput_StaysTranscode()
    {
        // Codec mismatch: ResolveAudio returns Transcode, so the downgrade must not fire.
        MediaInfo media = BuildAudioOnlyMedia("opus", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile([BuildAudioOutput(AudioCodecType.Aac)]);

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        AudioOutputPlan audio = Assert.Single(success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(StreamAction.Transcode);
        audio.EncoderName.Should().Be("libfdk_aac");
    }

    [Fact]
    public async Task LosslessSourceAgainstLossyOutput_StaysTranscode()
    {
        // Lossless source (flac) heading toward a lossy target (aac) must always
        // transcode, even though nothing else about the match would block it.
        MediaInfo media = BuildAudioOnlyMedia("flac", bitRateKbps: 900);
        EncodingProfile profile = BuildProfile(
            [BuildAudioOutput(AudioCodecType.Aac)],
            Container.Mkv
        );

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        AudioOutputPlan audio = Assert.Single(success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public async Task AacPlusEac3FromAacSource_CopiesAacKeepsTranscodingEac3()
    {
        // "Keep it per-output": one profile requesting AAC + E-AC-3 from a
        // single AAC source copies the AAC rendition and still transcodes
        // toward E-AC-3 (codec mismatch on that output alone).
        MediaInfo media = BuildAudioOnlyMedia("aac", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile([
            BuildAudioOutput(AudioCodecType.Aac),
            BuildAudioOutput(AudioCodecType.Eac3, 448),
        ]);

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        success.Value.OutputPlan.AudioOutputs.Should().HaveCount(2);
        success.Value.OutputPlan.AudioOutputs[0].Action.Should().Be(StreamAction.Copy);
        success.Value.OutputPlan.AudioOutputs[1].Action.Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public async Task OpusSourceAgainstOpusHlsTsOutput_ContainerBlocksDowngrade()
    {
        // Full codec/bitrate/channel match (Opus source, Opus-requested output)
        // must still not downgrade when the profile's own container cannot
        // carry the codec — HlsTs has no Opus entry in ContainerCompatibility,
        // mirroring the video HlsTs-can-only-carry-H264 guard.
        MediaInfo media = BuildAudioOnlyMedia("opus", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(
            [BuildAudioOutput(AudioCodecType.Opus)],
            Container.HlsTs
        );

        ValidateInput input = new(media, profile);
        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        AudioOutputPlan audio = Assert.Single(success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(StreamAction.Transcode);
    }
}
