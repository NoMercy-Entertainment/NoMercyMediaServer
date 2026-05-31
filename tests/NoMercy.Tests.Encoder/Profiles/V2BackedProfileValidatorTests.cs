using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// V2BackedProfileValidator combines the structured ProfileRuleValidator
/// with the legacy string-based ProfileValidator. The legacy strings are
/// bucketed under EncoderRuleId.ProfileNoOutputs so the dashboard can still
/// render them through the catalogued envelope shape — but the deep-link
/// drops to the bucket ID rather than the specific rule.
/// </summary>
public class V2BackedProfileValidatorTests
{
    private readonly V2BackedProfileValidator _validator = new();

    private static VideoOutput Video(
        VideoCodecType codec = VideoCodecType.H264,
        int width = 1920,
        int height = 1080,
        RateControlMode rc = RateControlMode.Crf,
        int crf = 23,
        int bitrate = 0
    ) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            Width: width,
            Height: height,
            RateControl: rc,
            Crf: crf,
            BitrateKbps: bitrate,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video_:framesize:/:framesize:_%05d",
            PlaylistNameTemplate: "video_:framesize:/playlist"
        );

    private static EncodingProfile Profile(
        VideoOutput? video,
        Container container = Container.HlsFmp4,
        AudioOutput[]? audio = null
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "v2-backed-test",
            Container: container,
            Video: video,
            Audio: audio ?? [],
            Subtitles: []
        );

    [Fact]
    public void Validate_ValidProfile_ReturnsValidResult()
    {
        EncodingProfile profile = Profile(Video());
        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_LegacyStringRuleFires_AsError()
    {
        // The non-envelope Validate() path only runs the legacy V2
        // ProfileValidator — its string errors get wrapped in
        // ValidationError entries with severity Error. The structured
        // ProfileRuleValidator only runs through ValidateAsEnvelope.
        AudioOutput audio = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 0,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["eng"],
            DefaultLanguage: "eng",
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio",
            PlaylistNameTemplate: "audio/p"
        );
        EncodingProfile profile = Profile(Video(), audio: [audio]);
        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateAsEnvelope_ValidProfile_ReturnsValidEnvelope()
    {
        EncodingProfile profile = Profile(Video());
        ValidationEnvelope env = _validator.ValidateAsEnvelope(profile);

        env.Valid.Should().BeTrue();
        env.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAsEnvelope_StructuredRuleFires_KeepsItsCataloguedId()
    {
        EncodingProfile profile = Profile(Video(rc: RateControlMode.Crf, crf: 0));
        ValidationEnvelope env = _validator.ValidateAsEnvelope(profile);

        env.Valid.Should().BeFalse();
        env.Errors.Select(r => r.Id)
            .Should()
            .Contain(
                EncoderRuleId.VideoRateControlMissing,
                "structured rules retain their catalogued IDs so the dashboard can deep-link"
            );
    }

    [Fact]
    public void ValidateAsEnvelope_LegacyStringFires_BucketedUnderProfileNoOutputs()
    {
        // Audio bitrate = 0 with codec AAC trips the legacy ProfileValidator
        // (ValidateAudioBitrate emits a string). That string gets wrapped in
        // an EncoderRule with the generic ProfileNoOutputs bucket id.
        AudioOutput audio = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 0, // <- triggers legacy string error
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["eng"],
            DefaultLanguage: "eng",
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio",
            PlaylistNameTemplate: "audio/p"
        );
        EncodingProfile profile = Profile(Video(), audio: [audio]);
        ValidationEnvelope env = _validator.ValidateAsEnvelope(profile);

        // Both layers fire at zero-bitrate AAC — the structured AudioBitrateMissing
        // rule AND the legacy string-bucketed entry. Verify both shapes surface.
        env.Errors.Select(r => r.Id).Should().Contain(EncoderRuleId.AudioBitrateMissing);
        // Legacy strings bucketed under ProfileNoOutputs id.
        env.Errors.Where(r => r.Id == EncoderRuleId.ProfileNoOutputs)
            .Should()
            .NotBeEmpty(
                "legacy ProfileValidator strings appear as ProfileNoOutputs-bucketed entries"
            );
    }

    [Fact]
    public void ValidateAsEnvelope_NoOutputs_TripsStructuredRule()
    {
        // ProfileNoOutputs is a structured-only rule — only the envelope
        // path catches it (legacy ProfileValidator doesn't have the check).
        EncodingProfile profile = Profile(video: null);
        ValidationEnvelope env = _validator.ValidateAsEnvelope(profile);

        env.Valid.Should().BeFalse();
        env.Errors.Select(r => r.Id).Should().Contain(EncoderRuleId.ProfileNoOutputs);
    }
}
