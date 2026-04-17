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
            BitrateKbps: 4000,
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
