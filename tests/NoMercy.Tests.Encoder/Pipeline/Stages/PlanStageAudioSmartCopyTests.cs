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
        _hardware.Setup(expression: h => h.HasGpu).Returns(value: false);
        _hardware.Setup(expression: h => h.CpuCores).Returns(value: 8);
        _hardware.Setup(expression: h => h.Gpus).Returns(value: []);

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

    private static MediaInfo BuildAudioOnlyMedia(
        string codec,
        int channels = 2,
        int sampleRate = 48000,
        long bitRateKbps = 192,
        string language = "eng"
    ) =>
        new(
            FilePath: "/music/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 3),
            OverallBitRateKbps: bitRateKbps,
            FileSizeBytes: 5_000_000,
            VideoStreams: [],
            AudioStreams:
            [
                new(
                    Index: 0,
                    Codec: codec,
                    Channels: channels,
                    SampleRate: sampleRate,
                    BitRateKbps: bitRateKbps,
                    Language: language,
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

    private static AudioOutput BuildAudioOutput(
        AudioCodecType codec,
        int bitrateKbps = 128,
        int channels = 2,
        int sampleRateHz = 48000
    ) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            BitrateKbps: bitrateKbps,
            Channels: channels,
            SampleRateHz: sampleRateHz,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}",
            PlaylistNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}"
        );

    private static EncodingProfile BuildProfile(
        AudioOutput[] audio,
        Container container = Container.HlsFmp4
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "AudioSmartCopy",
            Container: container,
            Video: null,
            Audio: audio,
            Subtitles: []
        );

    [Fact]
    public async Task AacSourceMatchingAacOutput_DowngradesToCopy()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(audio: [BuildAudioOutput(codec: AudioCodecType.Aac)]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Copy);
        audio.EncoderName.Should().Be(expected: "copy");
        audio
            .CodecToken.Should()
            .Be(
                expected: "aac",
                because: "the on-disk rendition must name the real source codec, not the literal \"copy\" pseudo-encoder"
            );

        _context
            .Decisions!.Snapshot()
            .Should()
            .Contain(predicate: entry => entry.Key == "plan.audio_smart_copy");
    }

    [Fact]
    public async Task OpusSourceAgainstAacRequestedOutput_StaysTranscode()
    {
        // Codec mismatch: ResolveAudio returns Transcode, so the downgrade must not fire.
        MediaInfo media = BuildAudioOnlyMedia(codec: "opus", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(audio: [BuildAudioOutput(codec: AudioCodecType.Aac)]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Transcode);
        audio.EncoderName.Should().Be(expected: "libfdk_aac");
    }

    [Fact]
    public async Task LosslessSourceAgainstLossyOutput_StaysTranscode()
    {
        // Lossless source (flac) heading toward a lossy target (aac) must always
        // transcode, even though nothing else about the match would block it.
        MediaInfo media = BuildAudioOnlyMedia(codec: "flac", bitRateKbps: 900);
        EncodingProfile profile = BuildProfile(
            audio: [BuildAudioOutput(codec: AudioCodecType.Aac)],
            container: Container.Mkv
        );

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public async Task AacPlusEac3FromAacSource_CopiesAacKeepsTranscodingEac3()
    {
        // "Keep it per-output": one profile requesting AAC + E-AC-3 from a
        // single AAC source copies the AAC rendition and still transcodes
        // toward E-AC-3 (codec mismatch on that output alone).
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: AudioCodecType.Aac),
            BuildAudioOutput(codec: AudioCodecType.Eac3, bitrateKbps: 448),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        success.Value.OutputPlan.AudioOutputs.Should().HaveCount(expected: 2);
        success.Value.OutputPlan.AudioOutputs[0].Action.Should().Be(expected: StreamAction.Copy);
        success.Value.OutputPlan.AudioOutputs[1].Action.Should().Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public async Task OpusSourceAgainstOpusHlsTsOutput_ContainerBlocksDowngrade()
    {
        // Full codec/bitrate/channel match (Opus source, Opus-requested output)
        // must still not downgrade when the profile's own container cannot
        // carry the codec — HlsTs has no Opus entry in ContainerCompatibility,
        // mirroring the video HlsTs-can-only-carry-H264 guard.
        MediaInfo media = BuildAudioOnlyMedia(codec: "opus", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(
            audio: [BuildAudioOutput(codec: AudioCodecType.Opus)],
            container: Container.HlsTs
        );

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Transcode);
    }
}
