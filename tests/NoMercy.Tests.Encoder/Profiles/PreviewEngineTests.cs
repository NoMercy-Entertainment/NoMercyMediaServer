namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Direct-call tests for <see cref="PreviewEngine.Analyze"/>.
/// No controller dependency — pure logic verification.
/// </summary>
public class PreviewEngineTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Fixture builders
    // ──────────────────────────────────────────────────────────────────────

    private static VideoStreamInfo MakeVideoStream(
        int index = 0,
        string codec = "h264",
        int width = 1920,
        int height = 1080,
        int bitDepth = 8,
        double frameRate = 24.0,
        double? averageFrameRate = null,
        double? realFrameRate = null
    ) =>
        new(
            Index: index,
            Codec: codec,
            Width: width,
            Height: height,
            FrameRate: frameRate,
            BitDepth: bitDepth,
            PixelFormat: bitDepth >= 10 ? "yuv420p10le" : "yuv420p",
            ColorPrimaries: null,
            ColorTransfer: null,
            ColorSpace: null,
            IsDefault: true,
            BitRateKbps: 5000,
            AverageFrameRate: averageFrameRate,
            RealFrameRate: realFrameRate
        );

    private static AudioStreamInfo MakeAudioStream(
        int index = 1,
        string codec = "aac",
        int channels = 2,
        long bitRateKbps = 192,
        string? language = "en"
    ) =>
        new(
            Index: index,
            Codec: codec,
            Channels: channels,
            SampleRate: 48000,
            BitRateKbps: bitRateKbps,
            Language: language,
            IsDefault: true,
            IsForced: false
        );

    private static SubtitleStreamInfo MakeSubtitleStream(
        int index = 2,
        string codec = "srt",
        string? language = "en"
    ) => new(Index: index, Codec: codec, Language: language, IsDefault: false, IsForced: false);

    private static MediaInfo MakeMediaInfo(
        VideoStreamInfo[]? video = null,
        AudioStreamInfo[]? audio = null,
        SubtitleStreamInfo[]? subtitles = null,
        DolbyVisionInfo? dolbyVision = null,
        TimeSpan? duration = null
    ) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska,webm",
            Duration: duration ?? TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 6000,
            FileSizeBytes: 4_000_000_000L,
            VideoStreams: video ?? [MakeVideoStream()],
            AudioStreams: audio ?? [MakeAudioStream()],
            SubtitleStreams: subtitles ?? [],
            Chapters: [],
            DolbyVision: dolbyVision
        );

    private static VideoOutput MakeVideoOutput(
        VideoCodecType codec = VideoCodecType.H264,
        int width = 1920,
        int? height = 1080,
        bool tenBit = false
    ) =>
        new(
            Codec: codec,
            Width: width,
            Height: height,
            BitrateKbps: 4000,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: tenBit
        );

    private static AudioOutput MakeAudioOutput(
        AudioCodecType codec = AudioCodecType.Aac,
        int channels = 2,
        int bitrateKbps = 192,
        string[]? languages = null
    ) =>
        new(
            Codec: codec,
            BitrateKbps: bitrateKbps,
            Channels: channels,
            SampleRateHz: 48000,
            AllowedLanguages: languages ?? ["en"]
        );

    private static EncodingProfile MakeProfile(
        VideoOutput[]? videoOutputs = null,
        AudioOutput[]? audioOutputs = null,
        SubtitleOutput[]? subtitleOutputs = null
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Test Profile",
            Format: NoMercy.Encoder.Codecs.OutputFormat.Hls,
            VideoOutputs: videoOutputs ?? [MakeVideoOutput()],
            AudioOutputs: audioOutputs ?? [MakeAudioOutput()],
            SubtitleOutputs: subtitleOutputs ?? []
        );

    // ──────────────────────────────────────────────────────────────────────
    // Dolby Vision warnings
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_emits_dv_warning_when_source_has_dv_and_target_is_h264()
    {
        DolbyVisionInfo dv = new(
            Profile: 5,
            Level: 6,
            HasRpu: true,
            HasEl: false,
            BlCompat: DvBlCompatibility.Hdr10
        );
        MediaInfo source = MakeMediaInfo(dolbyVision: dv);
        EncodingProfile profile = MakeProfile(
            videoOutputs: [MakeVideoOutput(codec: VideoCodecType.H264, tenBit: false)]
        );

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        result
            .SourceWarnings.Should()
            .Contain(r => r.Id == EncoderRuleId.SourceDolbyVisionWillBeStripped);
    }

    [Fact]
    public void Analyze_does_not_emit_dv_warning_when_target_is_hevc_main10()
    {
        DolbyVisionInfo dv = new(
            Profile: 5,
            Level: 6,
            HasRpu: true,
            HasEl: false,
            BlCompat: DvBlCompatibility.Hdr10
        );
        MediaInfo source = MakeMediaInfo(dolbyVision: dv);
        EncodingProfile profile = MakeProfile(
            videoOutputs: [MakeVideoOutput(codec: VideoCodecType.H265, tenBit: true)]
        );

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        result
            .SourceWarnings.Should()
            .NotContain(r => r.Id == EncoderRuleId.SourceDolbyVisionWillBeStripped);
    }

    // ──────────────────────────────────────────────────────────────────────
    // VFR warnings
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_emits_vfr_warning_when_real_avg_diverge()
    {
        // avg=24.0, real=23.5 → divergence ≈ 2 % > 1 % threshold
        VideoStreamInfo vfrStream = MakeVideoStream(averageFrameRate: 24.0, realFrameRate: 23.5);
        MediaInfo source = MakeMediaInfo(video: [vfrStream]);
        EncodingProfile profile = MakeProfile();

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        result.SourceWarnings.Should().Contain(r => r.Id == EncoderRuleId.SourceVariableFrameRate);
    }

    [Fact]
    public void Analyze_does_not_emit_vfr_warning_for_constant_fps()
    {
        // avg == real → zero divergence
        VideoStreamInfo cfrStream = MakeVideoStream(averageFrameRate: 24.0, realFrameRate: 24.0);
        MediaInfo source = MakeMediaInfo(video: [cfrStream]);
        EncodingProfile profile = MakeProfile();

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        result
            .SourceWarnings.Should()
            .NotContain(r => r.Id == EncoderRuleId.SourceVariableFrameRate);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Upscaling warning
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_emits_upscaling_warning_when_target_larger_than_source()
    {
        // Source is 1280×720, target asks for 1920×1080
        VideoStreamInfo sdStream = MakeVideoStream(width: 1280, height: 720);
        MediaInfo source = MakeMediaInfo(video: [sdStream]);
        EncodingProfile profile = MakeProfile(
            videoOutputs: [MakeVideoOutput(width: 1920, height: 1080)]
        );

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        result.SourceWarnings.Should().Contain(r => r.Id == EncoderRuleId.SourceUpscalingDetected);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Per-stream plan — audio
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_per_stream_plan_marks_unmatched_audio_as_skip()
    {
        // Source has a French audio stream; profile only allows English
        AudioStreamInfo frenchAudio = MakeAudioStream(index: 1, codec: "aac", language: "fr");
        MediaInfo source = MakeMediaInfo(audio: [frenchAudio]);
        EncodingProfile profile = MakeProfile(audioOutputs: [MakeAudioOutput(languages: ["en"])]);

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        AudioStreamAction frAction = result.Plan.AudioStreams.Should().ContainSingle().Subject;
        frAction.Action.Should().Be("skip");
    }

    [Fact]
    public void Analyze_per_stream_plan_marks_matching_audio_as_copy_or_transcode()
    {
        // Source matches the profile codec/bitrate → either copy or transcode depending on resolver
        AudioStreamInfo enAudio = MakeAudioStream(
            index: 1,
            codec: "aac",
            channels: 2,
            bitRateKbps: 256,
            language: "en"
        );
        MediaInfo source = MakeMediaInfo(audio: [enAudio]);
        EncodingProfile profile = MakeProfile(
            audioOutputs:
            [
                MakeAudioOutput(
                    codec: AudioCodecType.Aac,
                    channels: 2,
                    bitrateKbps: 192,
                    languages: ["en"]
                ),
            ]
        );

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        AudioStreamAction action = result.Plan.AudioStreams.Should().ContainSingle().Subject;
        action.Action.Should().BeOneOf("copy", "transcode");
    }

    // ──────────────────────────────────────────────────────────────────────
    // SourceAnalysisDto round-trip
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_source_analysis_dto_round_trips_video_metadata()
    {
        VideoStreamInfo video = MakeVideoStream(
            codec: "hevc",
            width: 3840,
            height: 2160,
            bitDepth: 10
        );
        MediaInfo source = MakeMediaInfo(video: [video], duration: TimeSpan.FromMinutes(120));
        EncodingProfile profile = MakeProfile();

        PreviewResult result = PreviewEngine.Analyze(profile, source);

        result.SourceAnalysis.PrimaryVideoCodec.Should().Be("hevc");
        result.SourceAnalysis.PrimaryVideoWidth.Should().Be(3840);
        result.SourceAnalysis.PrimaryVideoHeight.Should().Be(2160);
        result.SourceAnalysis.DurationSeconds.Should().BeApproximately(7200, 1);
        result.SourceAnalysis.Format.Should().Be("matroska,webm");
        result.EstimatedDurationSeconds.Should().Be(7200);
        result.EncoderHandle.Should().Be("auto");
        result.EstimatedFps.Should().Be(0);
    }
}
