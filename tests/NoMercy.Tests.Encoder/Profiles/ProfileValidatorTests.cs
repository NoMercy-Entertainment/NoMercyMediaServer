namespace NoMercy.Tests.Encoder.Profiles;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

public class ProfileValidatorTests
{
    private readonly ProfileValidator _validator = new(new CodecRegistry());

    // Helper: returns a valid HLS profile with H264 video + AAC audio + WebVTT subtitle
    private static EncodingProfile BuildValidProfile(
        OutputFormat format = OutputFormat.Hls,
        VideoOutput[]? videoOutputs = null,
        AudioOutput[]? audioOutputs = null,
        SubtitleOutput[]? subtitleOutputs = null,
        string name = "Test Profile"
    )
    {
        VideoOutput defaultVideo = new VideoOutput(
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

        AudioOutput defaultAudio = new AudioOutput(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );

        SubtitleOutput defaultSubtitle = new SubtitleOutput(
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
            SubtitleOutputs: subtitleOutputs ?? [defaultSubtitle]
        );
    }

    [Fact]
    public void ValidProfile_Passes()
    {
        EncodingProfile profile = BuildValidProfile();

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void EmptyProfile_NoOutputs_Fails()
    {
        EncodingProfile profile = BuildValidProfile(videoOutputs: [], audioOutputs: []);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e => e.Field == "Outputs" && e.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void EmptyName_Fails()
    {
        EncodingProfile profile = BuildValidProfile(name: "");

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e => e.Field == "Name" && e.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void VideoOutput_ZeroWidth_Fails()
    {
        VideoOutput badVideo = new VideoOutput(
            Codec: VideoCodecType.H264,
            Width: 0,
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

        EncodingProfile profile = BuildValidProfile(videoOutputs: [badVideo]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].Width" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void VideoOutput_NoBitrateNoCrf_Fails()
    {
        VideoOutput badVideo = new VideoOutput(
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

        EncodingProfile profile = BuildValidProfile(videoOutputs: [badVideo]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].RateControl" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void VideoOutput_CrfOutOfRange_Fails()
    {
        // H264 CRF max is 51; CRF=70 is out of range
        VideoOutput badVideo = new VideoOutput(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 0,
            Crf: 70,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        EncodingProfile profile = BuildValidProfile(videoOutputs: [badVideo]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].Crf" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void AudioOutput_InvalidChannels_Fails()
    {
        // AC3 supports [1, 2, 6] only — 8 channels not supported
        AudioOutput badAudio = new AudioOutput(
            Codec: AudioCodecType.Ac3,
            BitrateKbps: 384,
            Channels: 8,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );

        EncodingProfile profile = BuildValidProfile(audioOutputs: [badAudio]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "AudioOutput[0].Channels" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void AudioOutput_InvalidSampleRate_Fails()
    {
        // Opus only supports 48000 Hz — 96000 is invalid
        AudioOutput badAudio = new AudioOutput(
            Codec: AudioCodecType.Opus,
            BitrateKbps: 128,
            Channels: 2,
            SampleRateHz: 96000,
            AllowedLanguages: []
        );

        EncodingProfile profile = BuildValidProfile(audioOutputs: [badAudio]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "AudioOutput[0].SampleRateHz" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void AudioOutput_BitrateOutOfRange_Fails()
    {
        // AAC max bitrate is 512 kbps — 1000 is out of range
        AudioOutput badAudio = new AudioOutput(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 1000,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );

        EncodingProfile profile = BuildValidProfile(audioOutputs: [badAudio]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "AudioOutput[0].BitrateKbps" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void AudioOutput_Flac_ZeroBitrate_Passes()
    {
        // FLAC is lossless — zero bitrate is valid
        AudioOutput flacAudio = new AudioOutput(
            Codec: AudioCodecType.Flac,
            BitrateKbps: 0,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );

        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            audioOutputs: [flacAudio]
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Hls_WithVp9_Fails()
    {
        VideoOutput vp9Video = new VideoOutput(
            Codec: VideoCodecType.Vp9,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 0,
            Preset: null,
            Profile: null,
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Hls,
            videoOutputs: [vp9Video]
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].Codec" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void Hls_WithH265_Passes()
    {
        VideoOutput h265Video = new VideoOutput(
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 3000,
            Crf: 0,
            Preset: "medium",
            Profile: "main",
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Hls,
            videoOutputs: [h265Video]
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Mkv_WithAnything_Passes()
    {
        VideoOutput vp9Video = new VideoOutput(
            Codec: VideoCodecType.Vp9,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 0,
            Preset: null,
            Profile: null,
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        AudioOutput vorbisAudio = new AudioOutput(
            Codec: AudioCodecType.Vorbis,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );

        SubtitleOutput assSubtitle = new SubtitleOutput(
            Codec: SubtitleCodecType.Ass,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: []
        );

        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            videoOutputs: [vp9Video],
            audioOutputs: [vorbisAudio],
            subtitleOutputs: [assSubtitle]
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Mp4_WithVp9_Warning()
    {
        // VP9 in MP4 is non-standard — should warn, not error
        VideoOutput vp9Video = new VideoOutput(
            Codec: VideoCodecType.Vp9,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 0,
            Preset: null,
            Profile: null,
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mp4,
            videoOutputs: [vp9Video]
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].Codec" && e.Severity == ValidationSeverity.Warning
            );
    }

    [Fact]
    public void Hls_SubtitleAss_Warning()
    {
        // ASS in HLS Extract mode loses styling — should warn
        SubtitleOutput assSubtitle = new SubtitleOutput(
            Codec: SubtitleCodecType.Ass,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: []
        );

        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Hls,
            subtitleOutputs: [assSubtitle]
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "SubtitleOutput[0].Codec" && e.Severity == ValidationSeverity.Warning
            );
    }

    [Fact]
    public void TenBitH264_WarnsAboutNoHardwareSupport()
    {
        // H264 hardware encoders (NVENC/AMF/QSV/VAAPI/VideoToolbox) all lack
        // 10-bit support. Without an early warning, users configure a 10-bit
        // H264 profile expecting hardware acceleration and silently get
        // either software encoding or an 8-bit downgrade. The validator
        // surfaces this upfront.
        VideoOutput tenBitH264 = new VideoOutput(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 23,
            Preset: "medium",
            Profile: "high10",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: true
        );

        EncodingProfile profile = BuildValidProfile(videoOutputs: [tenBitH264]);
        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue("warning does not block the profile");
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].TenBit"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("hardware encoder")
            );
    }

    [Fact]
    public void TenBitH265_DoesNotWarn_HardwareSupportedAcrossVendors()
    {
        // HEVC is different from H264 — hevc_nvenc, hevc_amf, hevc_qsv,
        // hevc_vaapi all declare Supports10Bit=true (via main10 profile).
        // Only hevc_videotoolbox is 8-bit, which is handled at resolve/plan
        // time, not profile validation.
        VideoOutput tenBitH265 = new VideoOutput(
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 28,
            Preset: "medium",
            Profile: "main10",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: true
        );

        EncodingProfile profile = BuildValidProfile(videoOutputs: [tenBitH265]);
        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().NotContain(e => e.Field == "VideoOutput[0].TenBit");
    }

    [Fact]
    public void EightBitH264_NoTenBitWarning()
    {
        // Default profile is 8-bit H264 — no warning should ever surface.
        EncodingProfile profile = BuildValidProfile();
        ValidationResult result = _validator.Validate(profile);
        result.Errors.Should().NotContain(e => e.Field.EndsWith(".TenBit"));
    }

    [Fact]
    public void HlsSegmentNotMultipleOfKeyInterval_WarnsAboutDrift()
    {
        // 5s segment / 2s key interval → 5%2==1 → unaligned. Encoder will
        // insert an extra keyframe or drift segment length.
        VideoOutput video = new VideoOutput(
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

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Misaligned",
            Format: OutputFormat.Hls,
            VideoOutputs: [video],
            AudioOutputs:
            [
                new AudioOutput(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [],
            SegmentDurationSeconds: 5 // 5 % 2 != 0
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].KeyframeIntervalSeconds"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("does not evenly divide")
            );
    }

    [Fact]
    public void HlsSegmentAlignedToKeyInterval_NoWarning()
    {
        // Default profile is 6s segment / 2s keyint — 6%2==0.
        EncodingProfile profile = BuildValidProfile();
        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Field == "VideoOutput[0].KeyframeIntervalSeconds");
    }

    [Fact]
    public void MkvProfile_DoesNotWarnAboutSegmentAlignment()
    {
        // MKV isn't segmented — alignment doesn't matter. The validator must
        // not warn on MKV profiles with any keyframe interval / segment
        // duration combination.
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Mkv",
            Format: OutputFormat.Mkv,
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
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [],
            SegmentDurationSeconds: 5
        );

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Field == "VideoOutput[0].KeyframeIntervalSeconds");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Level vs dimensions — silent client-incompatibility trap
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void H264Level41_With1080p_Passes()
    {
        EncodingProfile profile = BuildValidProfile(); // already L4.1 + 1920x1080
        ValidationResult result = _validator.Validate(profile);
        result.Errors.Should().NotContain(e => e.Field.EndsWith(".Level"));
    }

    [Fact]
    public void H264Level41_With4K_Errors()
    {
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H264,
            width: 3840,
            height: 2160,
            level: "4.1"
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].Level" && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void H264Level51_With4K_Passes()
    {
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H264,
            width: 3840,
            height: 2160,
            level: "5.1"
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Field.EndsWith(".Level"));
    }

    [Fact]
    public void Hevc_Level40_With4K_Errors()
    {
        // HEVC L4.0 caps at 1080p. Requesting 4K at that level is a common
        // mistake (defaulting to L4.0 for everything).
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H265,
            width: 3840,
            height: 2160,
            level: "4.0",
            profile: "main10"
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "VideoOutput[0].Level");
    }

    [Fact]
    public void Level_NotSet_SkipsValidation()
    {
        // Level is optional — built-in presets leave it null so the encoder
        // picks the right level from source. Unset level must never trigger
        // the dimension check.
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H264,
            width: 3840,
            height: 2160,
            level: null
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Field.EndsWith(".Level"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // VP9 profile vs TenBit — profile0/1 are 8-bit only
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Vp9_Profile0_TenBit_Errors()
    {
        VideoOutput v = BuildVideo(codec: VideoCodecType.Vp9, profile: "profile0", tenBit: true);
        EncodingProfile profile = BuildValidProfile(format: OutputFormat.Dash, videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "VideoOutput[0].Profile");
    }

    [Fact]
    public void Vp9_Profile2_TenBit_Passes()
    {
        VideoOutput v = BuildVideo(codec: VideoCodecType.Vp9, profile: "profile2", tenBit: true);
        EncodingProfile profile = BuildValidProfile(format: OutputFormat.Dash, videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Where(e => e.Severity == ValidationSeverity.Error)
            .Should()
            .NotContain(e => e.Field == "VideoOutput[0].Profile");
    }

    [Fact]
    public void Vp9_Profile2_EightBit_Warns()
    {
        // profile2/3 imply 10-bit — mismatch between profile and TenBit
        // produces surprising output. Warn, don't hard-block (fix is one tick).
        VideoOutput v = BuildVideo(codec: VideoCodecType.Vp9, profile: "profile2", tenBit: false);
        EncodingProfile profile = BuildValidProfile(format: OutputFormat.Dash, videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutput[0].Profile" && e.Severity == ValidationSeverity.Warning
            );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Preset / Profile unknown to codec family — silent fallback warning
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Preset_NvencNativeOnH264_Warns()
    {
        // "p4" is NVENC's preset. If the user configured H264 with p4 and
        // hardware resolves to libx264, resolver falls back to middle preset
        // silently. Warn the user.
        VideoOutput v = BuildVideo(codec: VideoCodecType.H264, preset: "p4");
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        // p4 IS in h264_nvenc's preset list — so should NOT warn.
        result
            .Errors.Should()
            .NotContain(e =>
                e.Field == "VideoOutput[0].Preset" && e.Severity == ValidationSeverity.Warning
            );
    }

    [Fact]
    public void Preset_CompletelyUnknown_Warns()
    {
        VideoOutput v = BuildVideo(codec: VideoCodecType.H264, preset: "notarealpreset");
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.Field == "VideoOutput[0].Preset" && e.Severity == ValidationSeverity.Warning
            );
    }

    [Fact]
    public void Profile_UnknownToCodec_Warns()
    {
        VideoOutput v = BuildVideo(codec: VideoCodecType.H264, profile: "totally-invalid");
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutput[0].Profile" && e.Severity == ValidationSeverity.Warning
            );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Crf + Bitrate mutually exclusive
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CrfAndBitrate_BothSet_Warns()
    {
        VideoOutput v = BuildVideo(codec: VideoCodecType.H264, crf: 23, bitrateKbps: 4000);
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutput[0].RateControl"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("mutually exclusive")
            );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CustomArguments reserved flags
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("-c:v")]
    [InlineData("-preset")]
    [InlineData("-crf")]
    [InlineData("-b:v")]
    [InlineData("-pix_fmt")]
    [InlineData("-vf")]
    [InlineData("-init_hw_device")]
    public void CustomArguments_ReservedFlag_Errors(string flag)
    {
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H264,
            customArguments: new() { [flag] = "someValue" }
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutput[0].CustomArguments"
                && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void AudioCustomArguments_ReservedFlag_Errors()
    {
        // -b:a is an audio pipeline flag; user CustomArguments can't override it.
        AudioOutput audio = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            CustomArguments: new() { ["-b:a"] = "256k" }
        );
        EncodingProfile profile = BuildValidProfile(audioOutputs: [audio]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutput[0].CustomArguments"
                && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void SubtitleCustomArguments_ReservedFlag_Errors()
    {
        SubtitleOutput sub = new(
            Codec: SubtitleCodecType.WebVtt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: [],
            CustomArguments: new() { ["-c:s"] = "copy" }
        );
        EncodingProfile profile = BuildValidProfile(subtitleOutputs: [sub]);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "SubtitleOutput[0].CustomArguments"
                && e.Severity == ValidationSeverity.Error
            );
    }

    [Fact]
    public void CustomArguments_SafeFlag_Passes()
    {
        // -metadata is a real case: users tag files with their own metadata.
        // Not a pipeline-controlled flag, so it must NOT be rejected.
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H264,
            customArguments: new() { ["-metadata"] = "author=me" }
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Field == "VideoOutput[0].CustomArguments");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Duplicate + inverted ladder
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DuplicateVariants_Warn()
    {
        VideoOutput one = BuildVideo(codec: VideoCodecType.H264);
        VideoOutput two = BuildVideo(codec: VideoCodecType.H264); // identical
        EncodingProfile profile = BuildValidProfile(videoOutputs: [one, two]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutput[1]"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("Duplicate")
            );
    }

    [Fact]
    public void InvertedLadder_Warns()
    {
        // 1080p @ 2000 kbps below 720p @ 5000 kbps — broken ABR.
        VideoOutput low = BuildVideo(
            codec: VideoCodecType.H264,
            width: 1920,
            height: 1080,
            bitrateKbps: 2000,
            crf: 0
        );
        VideoOutput high = BuildVideo(
            codec: VideoCodecType.H264,
            width: 1280,
            height: 720,
            bitrateKbps: 5000,
            crf: 0
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [low, high]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Message.Contains("inverted") && e.Severity == ValidationSeverity.Warning
            );
    }

    [Fact]
    public void MonotonicLadder_Passes()
    {
        VideoOutput a = BuildVideo(
            codec: VideoCodecType.H264,
            width: 1280,
            height: 720,
            bitrateKbps: 3000,
            crf: 0
        );
        VideoOutput b = BuildVideo(
            codec: VideoCodecType.H264,
            width: 1920,
            height: 1080,
            bitrateKbps: 6000,
            crf: 0
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [a, b]);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("inverted"));
    }

    private static VideoOutput BuildVideo(
        VideoCodecType codec = VideoCodecType.H264,
        int width = 1920,
        int? height = 1080,
        int bitrateKbps = 0,
        int crf = 23,
        string? preset = "medium",
        string? profile = "high",
        string? level = "4.1",
        bool tenBit = false,
        Dictionary<string, string>? customArguments = null
    ) =>
        new(
            Codec: codec,
            Width: width,
            Height: height,
            BitrateKbps: bitrateKbps,
            Crf: crf,
            Preset: preset,
            Profile: profile,
            Level: level,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: tenBit,
            CustomArguments: customArguments
        );

    // ──────────────────────────────────────────────────────────────────────────
    // Video bitrate-per-resolution floor — "4K at 2 Mbps" disaster prevention
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void H264_4K_BelowBitrateFloor_Warns()
    {
        // 4K at 2000 kbps H.264 is famously unwatchable (~12 Mbps floor).
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H264,
            width: 3840,
            height: 2160,
            bitrateKbps: 2000,
            crf: 0,
            level: "5.1"
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutput[0].BitrateKbps"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("blocking")
            );
    }

    [Fact]
    public void Hevc_4K_ReasonableBitrate_NoWarning()
    {
        // HEVC 4K at 10 Mbps is solid quality — well above the 8 Mbps floor.
        VideoOutput v = BuildVideo(
            codec: VideoCodecType.H265,
            width: 3840,
            height: 2160,
            bitrateKbps: 10_000,
            crf: 0,
            profile: "main10",
            level: "5.1"
        );
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e =>
                e.Field == "VideoOutput[0].BitrateKbps" && e.Message.Contains("blocking")
            );
    }

    [Fact]
    public void CrfMode_DoesNotTriggerBitrateFloor()
    {
        // BitrateKbps=0 + Crf=23 is the normal CRF mode; no bitrate target
        // means no bitrate-floor check applies.
        EncodingProfile profile = BuildValidProfile(); // default is CRF 23, bitrate 0
        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("blocking"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CRF quality floor — CRF 0 = lossless trap, very high CRF = unwatchable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void VeryHighCrf_H264_Warns()
    {
        VideoOutput v = BuildVideo(codec: VideoCodecType.H264, crf: 40);
        EncodingProfile profile = BuildValidProfile(videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutput[0].Crf"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("blocky")
            );
    }

    [Fact]
    public void NormalCrf_H264_NoQualityWarning()
    {
        // CRF 23 is the H.264 default — solid quality, no warning expected.
        EncodingProfile profile = BuildValidProfile();
        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("blocky"));
        result.Errors.Should().NotContain(e => e.Message.Contains("lossless"));
    }

    [Fact]
    public void Av1_Crf35_InsideWiderRange_NoWarning()
    {
        // AV1 uses 0-63, so CRF 35 is mid-range — NOT in the "unwatchable"
        // zone (AV1's threshold starts at 50). Avoids cross-codec false
        // positives from reusing H.264 thresholds.
        VideoOutput v = BuildVideo(codec: VideoCodecType.Av1, crf: 35, profile: "main");
        EncodingProfile profile = BuildValidProfile(format: OutputFormat.Dash, videoOutputs: [v]);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("blocky"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC3 stepped bitrate ladder — ffmpeg rounds down silently off-ladder
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ac3_OnLadderBitrate_NoWarning()
    {
        AudioOutput audio = new(
            Codec: AudioCodecType.Ac3,
            BitrateKbps: 384,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            audioOutputs: [audio]
        );

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Where(e => e.Severity == ValidationSeverity.Warning)
            .Should()
            .NotContain(e =>
                e.Field == "AudioOutput[0].BitrateKbps" && e.Message.Contains("ladder")
            );
    }

    [Fact]
    public void Ac3_OffLadderBitrate_WarnsWithNearestValidRungs()
    {
        // 100 kbps is between rungs 96 and 112 — ffmpeg rounds to 96 without
        // warning. Validator must flag it and point at both neighbors.
        AudioOutput audio = new(
            Codec: AudioCodecType.Ac3,
            BitrateKbps: 100,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            audioOutputs: [audio]
        );

        ValidationResult result = _validator.Validate(profile);

        ValidationError warning = result.Errors.Single(e =>
            e.Field == "AudioOutput[0].BitrateKbps"
            && e.Severity == ValidationSeverity.Warning
            && e.Message.Contains("ladder")
        );
        warning.Message.Should().Contain("96");
        warning.Message.Should().Contain("112");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Channel-aware bitrate quality guidance
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ac3Surround_Below384_WarnsCompressed()
    {
        AudioOutput audio = new(
            Codec: AudioCodecType.Ac3,
            BitrateKbps: 320, // on-ladder but below Dolby's recommended floor
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            audioOutputs: [audio]
        );

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutput[0].BitrateKbps"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("compressed")
            );
    }

    [Fact]
    public void Ac3Surround_At448_NoCompressionWarning()
    {
        AudioOutput audio = new(
            Codec: AudioCodecType.Ac3,
            BitrateKbps: 448,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            audioOutputs: [audio]
        );

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("compressed"));
    }

    [Fact]
    public void AacStereo_Above256_WarnsWastedBandwidth()
    {
        AudioOutput audio = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 320,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(audioOutputs: [audio]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutput[0].BitrateKbps"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("wasted")
            );
    }

    [Fact]
    public void AacStereo_At192_NoWasteWarning()
    {
        // 192 kbps is inside the AAC sweet spot for stereo — no warning.
        EncodingProfile profile = BuildValidProfile(); // default = 192 kbps AAC stereo
        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("wasted"));
    }

    [Fact]
    public void AacSurround_Below256_WarnsUnderProvisioned()
    {
        AudioOutput audio = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192, // fine for stereo, too low for 5.1
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            audioOutputs: [audio]
        );

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutput[0].BitrateKbps"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("under-provisioned")
            );
    }

    [Fact]
    public void Eac3Surround_Below384_WarnsCompressed()
    {
        // Same surround-guidance kicks in for EAC3 as for AC3.
        AudioOutput audio = new(
            Codec: AudioCodecType.Eac3,
            BitrateKbps: 256,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            audioOutputs: [audio]
        );

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutput[0].BitrateKbps" && e.Message.Contains("compressed")
            );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Streaming-container audio requirement
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HlsProfile_NoAudioOutputs_Warns()
    {
        EncodingProfile profile = BuildValidProfile(format: OutputFormat.Hls, audioOutputs: []);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutputs"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("iOS")
            );
    }

    [Fact]
    public void DashProfile_NoAudioOutputs_Warns()
    {
        EncodingProfile profile = BuildValidProfile(format: OutputFormat.Dash, audioOutputs: []);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e => e.Field == "AudioOutputs" && e.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void MkvProfile_NoAudio_DoesNotTriggerStreamingWarning()
    {
        // MKV with video-only is a legitimate use case (screen recordings,
        // silent footage) — the streaming warning must NOT fire.
        EncodingProfile profile = BuildValidProfile(format: OutputFormat.Mkv, audioOutputs: []);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e => e.Field == "AudioOutputs" && e.Message.Contains("iOS"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Audio language-filter overlap — duplicate encode trap
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AudioOutputs_OverlappingEnglishFilter_Warns()
    {
        AudioOutput one = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        AudioOutput two = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en", "jp"]
        );
        EncodingProfile profile = BuildValidProfile(audioOutputs: [one, two]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutput[1].AllowedLanguages"
                && e.Severity == ValidationSeverity.Warning
                && e.Message.Contains("duplicate")
            );
    }

    [Fact]
    public void AudioOutputs_DisjointLanguageFilters_NoWarning()
    {
        AudioOutput one = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        AudioOutput two = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["jp"]
        );
        EncodingProfile profile = BuildValidProfile(audioOutputs: [one, two]);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("duplicate"));
    }

    [Fact]
    public void AudioOutputs_DifferentCodec_DoesNotTriggerOverlap()
    {
        // Legit intent: AAC stereo + EAC3 5.1 of the same language for
        // bandwidth tiering. Different codec × channel combo → not flagged.
        AudioOutput aac = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        AudioOutput eac3 = new(
            Codec: AudioCodecType.Eac3,
            BitrateKbps: 640,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );
        EncodingProfile profile = BuildValidProfile(audioOutputs: [aac, eac3]);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("duplicate"));
    }

    [Fact]
    public void AudioOutputs_EmptyFilterOverlapsAll_Warns()
    {
        // Empty AllowedLanguages means "all languages". With two identical
        // outputs both catching "all", they'll produce duplicate files.
        AudioOutput one = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        AudioOutput two = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );
        EncodingProfile profile = BuildValidProfile(audioOutputs: [one, two]);

        ValidationResult result = _validator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "AudioOutput[1].AllowedLanguages" && e.Message.Contains("duplicate")
            );
    }

    [Fact]
    public void AudioOnly_Profile_Passes()
    {
        // No video outputs — just audio — is valid as long as there is at least one output
        AudioOutput audio = new AudioOutput(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: []
        );

        EncodingProfile profile = BuildValidProfile(
            format: OutputFormat.Mkv,
            videoOutputs: [],
            audioOutputs: [audio],
            subtitleOutputs: []
        );

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
