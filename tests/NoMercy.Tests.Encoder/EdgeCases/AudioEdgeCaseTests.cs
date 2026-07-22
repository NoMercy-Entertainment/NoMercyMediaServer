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
using NoMercy.Tests.Encoder.Pipeline.Stages;
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;

namespace NoMercy.Tests.Encoder.EdgeCases;

public class AudioEdgeCaseTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly PlanStage _stage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public AudioEdgeCaseTests()
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
        int sampleRateHz = 48000,
        NoMercy.Encoder.Profiles.DownmixMode? downmixMode = null
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
            Downmix: downmixMode.HasValue ? new(Mode: downmixMode.Value, CustomPanMatrix: null) : null,
            SegmentNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}",
            PlaylistNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}"
        );

    private static EncodingProfile BuildProfile(
        AudioOutput[] audio,
        Container container = Container.HlsFmp4
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "AudioEdgeCase",
            Container: container,
            Video: null,
            Audio: audio,
            Subtitles: []
        );

    [Fact]
    public async Task AacSourceMatchingAacOutput_DowngradesToCopy()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: AudioCodecType.Aac, bitrateKbps: 192),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Copy);
        audio.EncoderName.Should().Be(expected: "copy");
    }

    [Fact]
    public async Task AacSourceLowerBitrate_TargetHigherBitrate_TranscodesForBitrateUpgrade()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", bitRateKbps: 128);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: AudioCodecType.Aac, bitrateKbps: 256),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Transcode);
        audio.BitrateKbps.Should().Be(expected: 256);
    }

    [Fact]
    public async Task MonoAudioToStereoProfile_TranscodesForChannelUpgrade()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", channels: 1, bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(audio: [BuildAudioOutput(codec: AudioCodecType.Aac, channels: 2)]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Transcode);
        audio.Channels.Should().Be(expected: 2);
    }

    [Fact]
    public async Task DownmixToStereo_AudioFilterIsItuR128Pan()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", channels: 6);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(
                codec: AudioCodecType.Aac,
                channels: 2,
                downmixMode: NoMercy.Encoder.Profiles.DownmixMode.StereoItuR128
            ),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.AudioFilter.Should().NotBeNullOrEmpty();
        audio.AudioFilter.Should().Contain(expected: "pan=stereo");
    }

    [Fact]
    public async Task DownmixToMono_AudioFilterIsMonoPan()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", channels: 6);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(
                codec: AudioCodecType.Aac,
                channels: 1,
                downmixMode: NoMercy.Encoder.Profiles.DownmixMode.Mono
            ),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.AudioFilter.Should().NotBeNullOrEmpty();
        audio.AudioFilter.Should().Contain(expected: "pan=mono");
    }

    [Fact]
    public async Task NoDownmixNoLoudness_AudioFilterIsNull()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "aac", channels: 2);
        EncodingProfile profile = BuildProfile(audio: [BuildAudioOutput(codec: AudioCodecType.Aac, channels: 2)]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.AudioFilter.Should().BeNull();
    }

    [Fact]
    public async Task OpusSourceMatchingOpusOutput_DowngradesToCopy()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "opus", channels: 2, bitRateKbps: 192);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: AudioCodecType.Opus, bitrateKbps: 192),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Copy);
        audio.EncoderName.Should().Be(expected: "copy");
    }

    [Fact]
    public async Task Eac3SourceMatchingEac3Output_DowngradesToCopy()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "eac3", channels: 6, bitRateKbps: 448);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: AudioCodecType.Eac3, bitrateKbps: 448, channels: 6),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Copy);
    }

    [Fact]
    public async Task TrueHdSourceToAacTranscode_ForcesTranscode()
    {
        MediaInfo media = BuildAudioOnlyMedia(codec: "truehd", channels: 6, bitRateKbps: 1200);
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: AudioCodecType.Aac, bitrateKbps: 384),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);
        audio.Action.Should().Be(expected: StreamAction.Transcode);
    }

    [Theory]
    [InlineData(data: ["aac", AudioCodecType.Aac, true])]
    [InlineData(data: ["ac3", AudioCodecType.Ac3, true])]
    [InlineData(data: ["eac3", AudioCodecType.Eac3, true])]
    [InlineData(data: ["opus", AudioCodecType.Opus, true])]
    [InlineData(data: ["unknown", AudioCodecType.Aac, false])]
    public async Task AudioCodecMatching_AcrossCodecsInHlsFmp4_ResolvesCopyOrTranscode(
        string sourceCodec,
        AudioCodecType targetCodec,
        bool shouldCopy
    )
    {
        int bitrate = targetCodec switch
        {
            AudioCodecType.Ac3 => 384,
            AudioCodecType.Eac3 => 448,
            _ => 192,
        };

        int channels = targetCodec switch
        {
            AudioCodecType.Ac3 => 6,
            AudioCodecType.Eac3 => 6,
            _ => 2,
        };

        MediaInfo media = BuildAudioOnlyMedia(
            codec: sourceCodec,
            channels: channels,
            bitRateKbps: bitrate
        );
        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: targetCodec, bitrateKbps: bitrate, channels: channels),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        AudioOutputPlan audio = Assert.Single(collection: success.Value.OutputPlan.AudioOutputs);

        if (shouldCopy)
            audio.Action.Should().Be(expected: StreamAction.Copy);
        else
            audio.Action.Should().Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public async Task MultipleAudioStreamsWithDifferentLanguages_PlansEachStream()
    {
        MediaInfo media = new(
            FilePath: "/music/multilang.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 3),
            OverallBitRateKbps: 600,
            FileSizeBytes: 5_000_000,
            VideoStreams: [],
            AudioStreams:
            [
                new(
                    Index: 0,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
                new(
                    Index: 1,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "spa",
                    IsDefault: false,
                    IsForced: false
                ),
                new(
                    Index: 2,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "fra",
                    IsDefault: false,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

        EncodingProfile profile = BuildProfile(audio:
        [
            BuildAudioOutput(codec: AudioCodecType.Aac, bitrateKbps: 192),
        ]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        success.Value.OutputPlan.AudioOutputs.Should().HaveCount(expected: 3);
        success.Value.OutputPlan.AudioOutputs[0].Language.Should().Be(expected: "eng");
        success.Value.OutputPlan.AudioOutputs[1].Language.Should().Be(expected: "spa");
        success.Value.OutputPlan.AudioOutputs[2].Language.Should().Be(expected: "fra");
    }

    [Fact]
    public async Task AudioWithLanguageFilter_IncludesOnlyAllowedLanguages()
    {
        MediaInfo media = new(
            FilePath: "/music/multilang.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 3),
            OverallBitRateKbps: 600,
            FileSizeBytes: 5_000_000,
            VideoStreams: [],
            AudioStreams:
            [
                new(
                    Index: 0,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
                new(
                    Index: 1,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "spa",
                    IsDefault: false,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

        AudioOutput audioOutput = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["eng"],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}",
            PlaylistNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}"
        );

        EncodingProfile profile = BuildProfile(audio: [audioOutput]);

        ValidateInput input = new(Media: media, Profile: profile);
        StageResult result = await _stage.ExecuteAsync(input: input, context: _context, ct: default);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        success.Value.OutputPlan.AudioOutputs.Should().HaveCount(expected: 1);
        success.Value.OutputPlan.AudioOutputs[0].Language.Should().Be(expected: "eng");
    }
}
