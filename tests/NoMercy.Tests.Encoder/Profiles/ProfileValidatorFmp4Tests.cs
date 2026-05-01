using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class ProfileValidatorFmp4Tests
{
    private readonly ProfileValidator _validator = new(new CodecRegistry());

    private static EncodingProfile BuildHlsFmp4Profile(VideoCodecType videoCodec) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "HLS fMP4 Test",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: videoCodec,
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
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"]
                ),
            ],
            SubtitleOutputs: []
        )
        {
            HlsOptions = new HlsOptions { SegmentType = "fmp4" },
        };

    private static EncodingProfile BuildHlsMpegtsProfile(VideoCodecType videoCodec) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "HLS mpegts Test",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: videoCodec,
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
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["en"]
                ),
            ],
            SubtitleOutputs: []
        )
        {
            HlsOptions = new HlsOptions { SegmentType = "mpegts" },
        };

    [Fact]
    public void Validate_emits_fmp4_codec_mismatch_for_vp9()
    {
        EncodingProfile profile = BuildHlsFmp4Profile(VideoCodecType.Vp9);

        ValidationEnvelope envelope = _validator.ValidateAsEnvelope(profile);

        Assert.Contains(
            envelope.Errors,
            r =>
                r.Id == EncoderRuleId.HlsFmp4CodecMismatch
                && r.Severity == EncoderRuleSeverity.Error
        );
    }

    [Fact]
    public void Validate_does_not_emit_for_h264()
    {
        EncodingProfile profile = BuildHlsFmp4Profile(VideoCodecType.H264);

        ValidationEnvelope envelope = _validator.ValidateAsEnvelope(profile);

        Assert.DoesNotContain(envelope.Errors, r => r.Id == EncoderRuleId.HlsFmp4CodecMismatch);
        Assert.DoesNotContain(envelope.Warnings, r => r.Id == EncoderRuleId.HlsFmp4CodecMismatch);
    }

    [Fact]
    public void Validate_does_not_emit_when_segment_type_mpegts()
    {
        // VP9 + mpegts → no fmp4 mismatch rule (mpegts allows VP9)
        EncodingProfile profile = BuildHlsMpegtsProfile(VideoCodecType.Vp9);

        ValidationEnvelope envelope = _validator.ValidateAsEnvelope(profile);

        Assert.DoesNotContain(envelope.Errors, r => r.Id == EncoderRuleId.HlsFmp4CodecMismatch);
        Assert.DoesNotContain(envelope.Warnings, r => r.Id == EncoderRuleId.HlsFmp4CodecMismatch);
    }
}
