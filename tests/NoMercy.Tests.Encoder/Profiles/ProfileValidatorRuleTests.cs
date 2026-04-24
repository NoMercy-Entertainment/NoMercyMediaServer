namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

/// <summary>
/// Per-rule tests for <see cref="ProfileValidator.ValidateAsEnvelope"/>.
/// Each rule ID gets two tests:
///   - fires_on_bad_input  — asserts the rule appears.
///   - does_not_fire_on_good_input — asserts the rule is absent (other rules may still fire).
/// </summary>
public class ProfileValidatorRuleTests
{
    private readonly ProfileValidator _validator = new(new CodecRegistry());

    // ──────────────────────────────────────────────────────────────────────
    // Baseline-valid profile builder — each test overrides only the
    // dimension under test.
    // ──────────────────────────────────────────────────────────────────────

    private static EncodingProfile ValidProfile(
        OutputFormat format = OutputFormat.Hls,
        VideoOutput[]? videoOutputs = null,
        AudioOutput[]? audioOutputs = null,
        SubtitleOutput[]? subtitleOutputs = null,
        string name = "Test Profile",
        int segmentDurationSeconds = 0
    )
    {
        VideoOutput defaultVideo = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        AudioOutput defaultAudio = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );

        SubtitleOutput defaultSubtitle = new(
            Codec: SubtitleCodecType.WebVtt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: ["en"]
        );

        return new EncodingProfile(
            Id: Ulid.NewUlid(),
            Name: name,
            Format: format,
            VideoOutputs: videoOutputs ?? [defaultVideo],
            AudioOutputs: audioOutputs ?? [defaultAudio],
            SubtitleOutputs: subtitleOutputs ?? [defaultSubtitle],
            SegmentDurationSeconds: segmentDurationSeconds
        );
    }

    private ValidationEnvelope Validate(EncodingProfile profile) =>
        _validator.ValidateAsEnvelope(profile);

    private static bool HasRule(ValidationEnvelope env, string id) =>
        env.Errors.Any(r => r.Id == id) || env.Warnings.Any(r => r.Id == id);

    // ──────────────────────────────────────────────────────────────────────
    // profile.name_missing
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ProfileNameMissing_fires_on_bad_input()
    {
        ValidationEnvelope env = Validate(ValidProfile(name: ""));

        HasRule(env, EncoderRuleId.ProfileNameMissing).Should().BeTrue();
    }

    [Fact]
    public void ProfileNameMissing_does_not_fire_on_good_input()
    {
        ValidationEnvelope env = Validate(ValidProfile(name: "My Profile"));

        HasRule(env, EncoderRuleId.ProfileNameMissing).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // profile.no_outputs
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ProfileNoOutputs_fires_on_bad_input()
    {
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [], audioOutputs: []));

        HasRule(env, EncoderRuleId.ProfileNoOutputs).Should().BeTrue();
    }

    [Fact]
    public void ProfileNoOutputs_does_not_fire_on_good_input()
    {
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.ProfileNoOutputs).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // video.width_invalid
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void VideoWidthInvalid_fires_on_bad_input()
    {
        VideoOutput bad = new(
            Codec: VideoCodecType.H264,
            Width: 0,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.VideoWidthInvalid).Should().BeTrue();
    }

    [Fact]
    public void VideoWidthInvalid_does_not_fire_on_good_input()
    {
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.VideoWidthInvalid).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // video.rate_control_missing
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void VideoRateControlMissing_fires_on_bad_input()
    {
        VideoOutput bad = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.VideoRateControlMissing).Should().BeTrue();
    }

    [Fact]
    public void VideoRateControlMissing_does_not_fire_on_good_input()
    {
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.VideoRateControlMissing).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // video.rate_control_conflict
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void VideoRateControlConflict_fires_on_bad_input()
    {
        VideoOutput bad = new(
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
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.VideoRateControlConflict).Should().BeTrue();
    }

    [Fact]
    public void VideoRateControlConflict_does_not_fire_on_good_input()
    {
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.VideoRateControlConflict).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // level.resolution_mismatch
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void LevelResolutionMismatch_fires_on_bad_input()
    {
        // 4K at Level 3.1 (max 983040 luma samples, ~1280x720)
        VideoOutput bad = new(
            Codec: VideoCodecType.H264,
            Width: 3840,
            Height: 2160,
            BitrateKbps: 0,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "3.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.LevelResolutionMismatch).Should().BeTrue();
    }

    [Fact]
    public void LevelResolutionMismatch_does_not_fire_on_good_input()
    {
        // 1080p at level 4.1 is fine
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.LevelResolutionMismatch).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // crf.out_of_typical_range
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void CrfOutOfTypicalRange_fires_on_bad_input()
    {
        VideoOutput bad = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 40,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.CrfOutOfTypicalRange).Should().BeTrue();
    }

    [Fact]
    public void CrfOutOfTypicalRange_does_not_fire_on_good_input()
    {
        ValidationEnvelope env = Validate(ValidProfile()); // Crf=23

        HasRule(env, EncoderRuleId.CrfOutOfTypicalRange).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // bitrate.too_low_for_resolution
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void BitrateTooLowForResolution_fires_on_bad_input()
    {
        VideoOutput bad = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 100,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.BitrateTooLowForResolution).Should().BeTrue();
    }

    [Fact]
    public void BitrateTooLowForResolution_does_not_fire_on_good_input()
    {
        VideoOutput good = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [good]));

        HasRule(env, EncoderRuleId.BitrateTooLowForResolution).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // bit_depth.vp9_profile_mismatch
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void BitDepthVp9ProfileMismatch_fires_on_bad_input()
    {
        VideoOutput bad = new(
            Codec: VideoCodecType.Vp9,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 33,
            Preset: "medium",
            Profile: "profile0",
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: true
        );
        ValidationEnvelope env = Validate(
            ValidProfile(format: OutputFormat.Mkv, videoOutputs: [bad])
        );

        HasRule(env, EncoderRuleId.BitDepthVp9ProfileMismatch).Should().BeTrue();
    }

    [Fact]
    public void BitDepthVp9ProfileMismatch_does_not_fire_on_good_input()
    {
        VideoOutput good = new(
            Codec: VideoCodecType.Vp9,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 33,
            Preset: "medium",
            Profile: "profile0",
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(
            ValidProfile(format: OutputFormat.Mkv, videoOutputs: [good])
        );

        HasRule(env, EncoderRuleId.BitDepthVp9ProfileMismatch).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // bit_depth.no_hardware_support
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void BitDepthNoHardwareSupport_fires_on_bad_input()
    {
        // H264 10-bit has no hardware support
        VideoOutput bad = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: true
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.BitDepthNoHardwareSupport).Should().BeTrue();
    }

    [Fact]
    public void BitDepthNoHardwareSupport_does_not_fire_on_good_input()
    {
        // 8-bit H264 — no hardware 10-bit warning
        ValidationEnvelope env = Validate(ValidProfile()); // TenBit: false

        HasRule(env, EncoderRuleId.BitDepthNoHardwareSupport).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // ladder.inverted
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void LadderInverted_fires_on_bad_input()
    {
        VideoOutput low = new(
            Codec: VideoCodecType.H264,
            Width: 1280,
            Height: 720,
            BitrateKbps: 5000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        VideoOutput high = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 2000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(
            ValidProfile(format: OutputFormat.Hls, videoOutputs: [low, high])
        );

        HasRule(env, EncoderRuleId.LadderInverted).Should().BeTrue();
    }

    [Fact]
    public void LadderInverted_does_not_fire_on_good_input()
    {
        VideoOutput low = new(
            Codec: VideoCodecType.H264,
            Width: 1280,
            Height: 720,
            BitrateKbps: 2000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        VideoOutput high = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 5000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(
            ValidProfile(format: OutputFormat.Hls, videoOutputs: [low, high])
        );

        HasRule(env, EncoderRuleId.LadderInverted).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // ladder.duplicate_variant
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void LadderDuplicateVariant_fires_on_bad_input()
    {
        VideoOutput v = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [v, v]));

        HasRule(env, EncoderRuleId.LadderDuplicateVariant).Should().BeTrue();
    }

    [Fact]
    public void LadderDuplicateVariant_does_not_fire_on_good_input()
    {
        VideoOutput v1 = new(
            Codec: VideoCodecType.H264,
            Width: 1280,
            Height: 720,
            BitrateKbps: 2000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        VideoOutput v2 = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 5000,
            Crf: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(
            ValidProfile(format: OutputFormat.Hls, videoOutputs: [v1, v2])
        );

        HasRule(env, EncoderRuleId.LadderDuplicateVariant).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // hls.keyframe_segment_misalignment
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void HlsKeyframeSegmentMisalignment_fires_on_bad_input()
    {
        // Segment=6, keyframe=4 → 6 % 4 != 0
        VideoOutput v = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 4,
            TenBit: false
        );
        ValidationEnvelope env = Validate(
            ValidProfile(format: OutputFormat.Hls, videoOutputs: [v], segmentDurationSeconds: 6)
        );

        HasRule(env, EncoderRuleId.HlsKeyframeSegmentMisalignment).Should().BeTrue();
    }

    [Fact]
    public void HlsKeyframeSegmentMisalignment_does_not_fire_on_good_input()
    {
        // Default profile: KeyframeIntervalSeconds=2, segmentDurationSeconds=0 → no check
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.HlsKeyframeSegmentMisalignment).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // custom_args.reserved_flag
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void CustomArgsReservedFlag_fires_on_bad_input()
    {
        VideoOutput bad = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false,
            CustomArguments: new Dictionary<string, string> { ["-crf"] = "28" }
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [bad]));

        HasRule(env, EncoderRuleId.CustomArgsReservedFlag).Should().BeTrue();
    }

    [Fact]
    public void CustomArgsReservedFlag_does_not_fire_on_good_input()
    {
        VideoOutput good = new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false,
            CustomArguments: new Dictionary<string, string> { ["-x264opts"] = "no-dct-decimate" }
        );
        ValidationEnvelope env = Validate(ValidProfile(videoOutputs: [good]));

        HasRule(env, EncoderRuleId.CustomArgsReservedFlag).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // audio.ac3_off_ladder_bitrate
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void AudioAc3OffLadderBitrate_fires_on_bad_input()
    {
        // AC3 ladder: 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 448, 640
        // 300 is not on the ladder.
        AudioOutput bad = new(
            Codec: AudioCodecType.Ac3,
            BitrateKbps: 300,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        ValidationEnvelope env = Validate(ValidProfile(audioOutputs: [bad]));

        HasRule(env, EncoderRuleId.AudioAc3OffLadderBitrate).Should().BeTrue();
    }

    [Fact]
    public void AudioAc3OffLadderBitrate_does_not_fire_on_good_input()
    {
        AudioOutput good = new(
            Codec: AudioCodecType.Ac3,
            BitrateKbps: 384,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        ValidationEnvelope env = Validate(ValidProfile(audioOutputs: [good]));

        HasRule(env, EncoderRuleId.AudioAc3OffLadderBitrate).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // audio.eac3_off_ladder_bitrate
    //
    // NOTE: The current EAC3 AudioEncoderInfo has no ValidBitrateLadder
    // (EAC3 accepts a free range of 32–6144 kbps). The rule ID is reserved
    // for when the codec registry adds a ladder. Until then the rule must
    // NEVER fire — two "does not fire" tests confirm the guard is silent.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void AudioEac3OffLadderBitrate_does_not_fire_on_off_ladder_value()
    {
        // EAC3 has no defined ladder → any bitrate in range is valid; rule stays silent.
        AudioOutput output = new(
            Codec: AudioCodecType.Eac3,
            BitrateKbps: 300,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        ValidationEnvelope env = Validate(ValidProfile(audioOutputs: [output]));

        HasRule(env, EncoderRuleId.AudioEac3OffLadderBitrate).Should().BeFalse();
    }

    [Fact]
    public void AudioEac3OffLadderBitrate_does_not_fire_on_standard_value()
    {
        AudioOutput good = new(
            Codec: AudioCodecType.Eac3,
            BitrateKbps: 384,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        ValidationEnvelope env = Validate(ValidProfile(audioOutputs: [good]));

        HasRule(env, EncoderRuleId.AudioEac3OffLadderBitrate).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // codec.container_mismatch  (VP9 in MP4)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void CodecContainerMismatch_fires_on_bad_input()
    {
        VideoOutput bad = new(
            Codec: VideoCodecType.Vp9,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 33,
            Preset: "medium",
            Profile: "profile0",
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        ValidationEnvelope env = Validate(
            ValidProfile(format: OutputFormat.Mp4, videoOutputs: [bad])
        );

        HasRule(env, EncoderRuleId.CodecContainerMismatch).Should().BeTrue();
    }

    [Fact]
    public void CodecContainerMismatch_does_not_fire_on_good_input()
    {
        // H264 in MP4 is standard
        ValidationEnvelope env = Validate(ValidProfile(format: OutputFormat.Mp4));

        HasRule(env, EncoderRuleId.CodecContainerMismatch).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // audio.codec_container_mismatch  (wrong codec for HLS)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void AudioCodecContainerMismatch_fires_on_bad_input()
    {
        // Vorbis is not allowed in HLS
        AudioOutput bad = new(
            Codec: AudioCodecType.Vorbis,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        ValidationEnvelope env = Validate(ValidProfile(audioOutputs: [bad]));

        HasRule(env, EncoderRuleId.AudioCodecContainerMismatch).Should().BeTrue();
    }

    [Fact]
    public void AudioCodecContainerMismatch_does_not_fire_on_good_input()
    {
        // AAC is valid in HLS
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.AudioCodecContainerMismatch).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // subtitles.container_incompatible
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubtitlesContainerIncompatible_fires_on_bad_input()
    {
        // SRT in HLS Extract mode is not allowed (only WebVTT / ASS-warning)
        SubtitleOutput bad = new(
            Codec: SubtitleCodecType.Srt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: ["en"]
        );
        ValidationEnvelope env = Validate(ValidProfile(subtitleOutputs: [bad]));

        HasRule(env, EncoderRuleId.SubtitlesContainerIncompatible).Should().BeTrue();
    }

    [Fact]
    public void SubtitlesContainerIncompatible_does_not_fire_on_good_input()
    {
        // WebVTT in HLS — valid
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.SubtitlesContainerIncompatible).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // subtitles.ass_needs_capable_client  (ASS → WebVTT warning in HLS)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubtitlesAssNeedsCapableClient_fires_on_bad_input()
    {
        SubtitleOutput bad = new(
            Codec: SubtitleCodecType.Ass,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: ["en"]
        );
        ValidationEnvelope env = Validate(ValidProfile(subtitleOutputs: [bad]));

        HasRule(env, EncoderRuleId.SubtitlesAssNeedsCapableClient).Should().BeTrue();
    }

    [Fact]
    public void SubtitlesAssNeedsCapableClient_does_not_fire_on_good_input()
    {
        ValidationEnvelope env = Validate(ValidProfile()); // WebVTT

        HasRule(env, EncoderRuleId.SubtitlesAssNeedsCapableClient).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // output.path_not_allowed  (path traversal via ValidatePathSafety)
    // Tested indirectly — the hook fires when a future HdrOptions.CustomLutPath
    // is set. Until that field exists the method is exercised via the
    // public API: the rule must NOT fire on a clean profile.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void OutputPathNotAllowed_does_not_fire_on_clean_profile()
    {
        ValidationEnvelope env = Validate(ValidProfile());

        HasRule(env, EncoderRuleId.OutputPathNotAllowed).Should().BeFalse();
    }
}
