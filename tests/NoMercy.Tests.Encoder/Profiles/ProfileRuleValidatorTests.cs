using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
///     Tests for the catalogued <see cref="EncoderRuleId"/> entries that the new
///     <see cref="ProfileRuleValidator"/> emits. Each rule gets a positive case (rule fires) and a
///     negative case (rule stays quiet) so future refactors keep the precision tight.
/// </summary>
public class ProfileRuleValidatorTests
{
    // ── Shared builders ───────────────────────────────────────────────────────

    private static VideoOutput Video(
        VideoCodecType codec = VideoCodecType.H264,
        int width = 1920,
        int? height = 1080,
        RateControlMode rc = RateControlMode.Crf,
        int crf = 23,
        int bitrate = 0,
        string? level = null,
        int bitDepth = 8,
        int keyframeSeconds = 2
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
            Level: level,
            Tune: null,
            BitDepth: bitDepth,
            PixelFormat: null,
            KeyframeIntervalSeconds: keyframeSeconds,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video_:framesize:/:framesize:_%05d",
            PlaylistNameTemplate: "video_:framesize:/playlist"
        );

    private static EncodingProfile ProfileFor(
        VideoOutput? video = null,
        Container container = Container.HlsFmp4,
        int segmentDurationSeconds = 6,
        LadderConfig? ladder = null,
        AudioOutput[]? audio = null,
        SubtitleOutput[]? subtitles = null,
        HdrPolicy hdrPolicy = HdrPolicy.PassthroughWhenPossible
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "rule-validator-test",
            Container: container,
            Video: video,
            Audio: audio ?? [],
            Subtitles: subtitles ?? [],
            SegmentDurationSeconds: segmentDurationSeconds,
            Ladder: ladder
        )
        {
            HdrPolicy = hdrPolicy,
        };

    private static AudioOutput Audio(AudioCodecType codec, int bitrate) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            BitrateKbps: bitrate,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["eng"],
            DefaultLanguage: "eng",
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio_:lang:_:codec:/:lang:_:codec:_%05d",
            PlaylistNameTemplate: "audio_:lang:_:codec:/playlist"
        );

    private static SubtitleOutput Subtitle(SubtitleCodecType codec, SubtitlePolicy policy) =>
        new(
            Policy: policy,
            Codec: codec,
            AllowedLanguages: ["eng"],
            IncludeForced: true,
            OcrLanguage: "eng",
            PlaylistNameTemplate: "subs/:lang:"
        );

    private static bool HasRule(ValidationEnvelope env, string id) =>
        env.Errors.Any(r => r.Id == id) || env.Warnings.Any(r => r.Id == id);

    // ── LevelResolutionMismatch ──────────────────────────────────────────────

    [Fact]
    public void LevelResolutionMismatch_4kAtLevel4_0_Fires()
    {
        EncodingProfile profile = ProfileFor(
            Video(codec: VideoCodecType.H264, width: 3840, height: 2160, level: "4.0")
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);

        Assert.True(HasRule(env, EncoderRuleId.LevelResolutionMismatch));
        Assert.False(env.Valid);
    }

    [Fact]
    public void LevelResolutionMismatch_1080pAtLevel4_1_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(
            Video(codec: VideoCodecType.H264, width: 1920, height: 1080, level: "4.1")
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);

        Assert.False(HasRule(env, EncoderRuleId.LevelResolutionMismatch));
    }

    [Fact]
    public void LevelResolutionMismatch_NoLevel_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(Video(width: 3840, height: 2160));
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.LevelResolutionMismatch));
    }

    // ── BitrateTooLowForResolution ───────────────────────────────────────────

    [Fact]
    public void BitrateTooLowForResolution_4kAt500kbps_Fires()
    {
        EncodingProfile profile = ProfileFor(
            Video(width: 3840, height: 2160, rc: RateControlMode.Vbr, bitrate: 500)
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);

        Assert.True(HasRule(env, EncoderRuleId.BitrateTooLowForResolution));
        EncoderRule rule = env.Warnings.First(r =>
            r.Id == EncoderRuleId.BitrateTooLowForResolution
        );
        Assert.Equal(EncoderRuleSeverity.Warning, rule.Severity);
    }

    [Fact]
    public void BitrateTooLowForResolution_1080pAt3000kbps_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(
            Video(width: 1920, height: 1080, rc: RateControlMode.Vbr, bitrate: 3000)
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.BitrateTooLowForResolution));
    }

    [Fact]
    public void BitrateTooLowForResolution_CrfMode_DoesNotFire()
    {
        // CRF profiles don't pin a bitrate target, so the rule shouldn't fire.
        EncodingProfile profile = ProfileFor(
            Video(width: 3840, height: 2160, rc: RateControlMode.Crf, crf: 23)
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.BitrateTooLowForResolution));
    }

    [Theory]
    [InlineData(3840, 8000)]
    [InlineData(2560, 5000)]
    [InlineData(1920, 2500)]
    [InlineData(1280, 1500)]
    [InlineData(854, 700)]
    [InlineData(640, 300)]
    public void MinimumBitrateKbpsFor_MatchesLadder(int width, int expectedMin)
    {
        Assert.Equal(expectedMin, ProfileRuleValidator.MinimumBitrateKbpsFor(width));
    }

    // ── CrfOutOfTypicalRange ─────────────────────────────────────────────────

    [Theory]
    [InlineData(5)] // very low (huge files)
    [InlineData(35)] // very high (heavy artefacts)
    [InlineData(51)] // boundary
    [InlineData(0)] // boundary
    public void CrfOutOfTypicalRange_FiresOutsideSeventeenTwentyEight(int crf)
    {
        EncodingProfile profile = ProfileFor(Video(rc: RateControlMode.Crf, crf: crf));

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);

        Assert.True(HasRule(env, EncoderRuleId.CrfOutOfTypicalRange));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(23)]
    [InlineData(28)]
    public void CrfOutOfTypicalRange_QuietInsideSeventeenTwentyEight(int crf)
    {
        EncodingProfile profile = ProfileFor(Video(rc: RateControlMode.Crf, crf: crf));
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.CrfOutOfTypicalRange));
    }

    [Fact]
    public void CrfOutOfTypicalRange_NonCrfModes_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(Video(rc: RateControlMode.Vbr, crf: 5, bitrate: 4000));
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.CrfOutOfTypicalRange));
    }

    [Fact]
    public void CrfOutOfTypicalRange_Av1_DoesNotFireOnH264TypicalRange()
    {
        // The rule is scoped to H.264/H.265 since AV1's CRF semantics differ. Should stay quiet
        // on AV1 even if the value would be flagged for H.264.
        EncodingProfile profile = ProfileFor(
            Video(codec: VideoCodecType.Av1, rc: RateControlMode.Crf, crf: 5)
        );
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.CrfOutOfTypicalRange));
    }

    // ── HlsKeyframeSegmentMisalignment ───────────────────────────────────────

    [Fact]
    public void HlsKeyframeSegmentMisalignment_3sKeyframe6sSegment_Fires()
    {
        EncodingProfile profile = ProfileFor(
            Video(keyframeSeconds: 3),
            container: Container.HlsFmp4,
            segmentDurationSeconds: 6
        );
        // wait — 3 divides 6 evenly, no misalignment. Use 4.
        profile = ProfileFor(
            Video(keyframeSeconds: 4),
            container: Container.HlsFmp4,
            segmentDurationSeconds: 6
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(HasRule(env, EncoderRuleId.HlsKeyframeSegmentMisalignment));
    }

    [Fact]
    public void HlsKeyframeSegmentMisalignment_2sKeyframe6sSegment_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(
            Video(keyframeSeconds: 2),
            container: Container.HlsFmp4,
            segmentDurationSeconds: 6
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.HlsKeyframeSegmentMisalignment));
    }

    [Fact]
    public void HlsKeyframeSegmentMisalignment_NonHlsContainer_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(
            Video(keyframeSeconds: 4),
            container: Container.Mp4,
            segmentDurationSeconds: 6
        );
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.HlsKeyframeSegmentMisalignment));
    }

    // ── LadderInverted ───────────────────────────────────────────────────────

    [Fact]
    public void LadderInverted_HigherWidthLowerBitrate_Fires()
    {
        LadderRung[] rungs =
        [
            new(854, 480, VideoCodecType.H264, 4000, 4800, 8000, 24), // 480p @ 4 Mbps (high)
            new(1920, 1080, VideoCodecType.H264, 2000, 2400, 4000, 24), // 1080p @ 2 Mbps (lower!)
        ];
        EncodingProfile profile = ProfileFor(
            Video(),
            ladder: new() { Mode = LadderMode.Manual, Rungs = rungs }
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(HasRule(env, EncoderRuleId.LadderInverted));
        Assert.False(env.Valid);
    }

    [Fact]
    public void LadderInverted_AscendingBitrate_DoesNotFire()
    {
        LadderRung[] rungs =
        [
            new(854, 480, VideoCodecType.H264, 1000, 1200, 2000, 24),
            new(1280, 720, VideoCodecType.H264, 2500, 3000, 5000, 24),
            new(1920, 1080, VideoCodecType.H264, 4500, 5400, 9000, 24),
        ];
        EncodingProfile profile = ProfileFor(
            Video(),
            ladder: new() { Mode = LadderMode.Manual, Rungs = rungs }
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.LadderInverted));
    }

    // ── AudioAc3OffLadderBitrate ─────────────────────────────────────────────

    [Fact]
    public void AudioAc3OffLadderBitrate_Ac3At333kbps_Fires()
    {
        EncodingProfile profile = ProfileFor(audio: [Audio(AudioCodecType.Ac3, 333)]);
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(HasRule(env, EncoderRuleId.AudioAc3OffLadderBitrate));
    }

    [Fact]
    public void AudioAc3OffLadderBitrate_Ac3At320kbps_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(audio: [Audio(AudioCodecType.Ac3, 320)]);
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.AudioAc3OffLadderBitrate));
    }

    [Fact]
    public void AudioAc3OffLadderBitrate_AacAnyBitrate_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(audio: [Audio(AudioCodecType.Aac, 137)]);
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.AudioAc3OffLadderBitrate));
    }

    [Fact]
    public void AudioEac3OffLadderBitrate_OffLadder_Fires()
    {
        EncodingProfile profile = ProfileFor(audio: [Audio(AudioCodecType.Eac3, 137)]);
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(HasRule(env, EncoderRuleId.AudioEac3OffLadderBitrate));
    }

    // ── SubtitlesContainerIncompatible ───────────────────────────────────────

    [Fact]
    public void SubtitlesContainerIncompatible_AssInMp4_Fires()
    {
        EncodingProfile profile = ProfileFor(
            Video(),
            container: Container.Mp4,
            subtitles: [Subtitle(SubtitleCodecType.Ass, SubtitlePolicy.Extract)]
        );
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(HasRule(env, EncoderRuleId.SubtitlesContainerIncompatible));
        Assert.False(env.Valid);
    }

    [Fact]
    public void SubtitlesContainerIncompatible_WebVttInHls_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(
            Video(),
            container: Container.HlsFmp4,
            subtitles: [Subtitle(SubtitleCodecType.WebVtt, SubtitlePolicy.Extract)]
        );
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.SubtitlesContainerIncompatible));
    }

    [Fact]
    public void SubtitlesContainerIncompatible_BurnInIgnored()
    {
        // BurnIn renders into video pixels; container compatibility doesn't matter.
        EncodingProfile profile = ProfileFor(
            Video(),
            container: Container.Mp4,
            subtitles: [Subtitle(SubtitleCodecType.Ass, SubtitlePolicy.BurnIn)]
        );
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.SubtitlesContainerIncompatible));
    }

    // ── HdrInverseTonemapUnsupported ─────────────────────────────────────────

    [Fact]
    public void HdrInverseTonemapUnsupported_PreserveOn8Bit_Fires()
    {
        EncodingProfile profile = ProfileFor(
            Video(bitDepth: 8),
            hdrPolicy: HdrPolicy.AlwaysPreserve
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(HasRule(env, EncoderRuleId.HdrInverseTonemapUnsupported));
        Assert.False(env.Valid);
    }

    [Fact]
    public void HdrInverseTonemapUnsupported_PreserveOn10Bit_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(
            Video(bitDepth: 10),
            hdrPolicy: HdrPolicy.AlwaysPreserve
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.HdrInverseTonemapUnsupported));
    }

    [Fact]
    public void HdrInverseTonemapUnsupported_PassthroughPolicy_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(
            Video(bitDepth: 8),
            hdrPolicy: HdrPolicy.PassthroughWhenPossible
        );
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.HdrInverseTonemapUnsupported));
    }

    // ── CustomArgsReservedFlag ───────────────────────────────────────────────

    [Theory]
    [InlineData("-c:v")]
    [InlineData("-preset")]
    [InlineData("-hwaccel")]
    [InlineData("-map")]
    [InlineData("-vf")]
    [InlineData("-hls_time")]
    [InlineData("-hls_segment_filename")]
    [InlineData("-filter_complex")]
    [InlineData("-init_hw_device")]
    public void CustomArgsReservedFlag_FiresAsError(string flag)
    {
        EncodingProfile profile = ProfileFor(Video()) with
        {
            CustomArguments = new Dictionary<string, string> { [flag] = "anything" },
        };

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        EncoderRule rule = env.Errors.First(r => r.Id == EncoderRuleId.CustomArgsReservedFlag);
        Assert.Equal(EncoderRuleSeverity.Error, rule.Severity);
        Assert.Contains(flag, rule.Field);
        Assert.False(env.Valid);
    }

    [Fact]
    public void CustomArgsReservedFlag_AllowsKeyWithoutLeadingDash()
    {
        // Some callers store keys as "c:v" (without dash). Normalize before checking.
        EncodingProfile profile = ProfileFor(Video()) with
        {
            CustomArguments = new Dictionary<string, string> { ["c:v"] = "libx264" },
        };

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(HasRule(env, EncoderRuleId.CustomArgsReservedFlag));
    }

    [Fact]
    public void CustomArgsReservedFlag_AllowsNonReservedFlag()
    {
        // -loglevel is informational, not derived from profile fields. Permitted.
        EncodingProfile profile = ProfileFor(Video()) with
        {
            CustomArguments = new Dictionary<string, string> { ["-loglevel"] = "info" },
        };

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.CustomArgsReservedFlag));
    }

    [Fact]
    public void CustomArgsReservedFlag_NoCustomArgs_DoesNotFire()
    {
        EncodingProfile profile = ProfileFor(Video());
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(HasRule(env, EncoderRuleId.CustomArgsReservedFlag));
    }

    // ── Envelope integrity ───────────────────────────────────────────────────

    [Fact]
    public void Validate_NoRules_ReturnsValidEmptyEnvelope()
    {
        EncodingProfile profile = ProfileFor(Video(keyframeSeconds: 2));
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(env.Valid);
        Assert.Empty(env.Errors);
        Assert.Empty(env.Warnings);
    }

    [Fact]
    public void Validate_ErrorPresent_EnvelopeInvalid()
    {
        EncodingProfile profile = ProfileFor(
            Video(width: 3840, height: 2160, level: "4.0") // LevelResolutionMismatch
        );
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.False(env.Valid);
        Assert.NotEmpty(env.Errors);
    }

    [Fact]
    public void Validate_OnlyWarnings_EnvelopeStillValid()
    {
        EncodingProfile profile = ProfileFor(Video(rc: RateControlMode.Crf, crf: 5));
        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        Assert.True(env.Valid);
        Assert.NotEmpty(env.Warnings);
    }

    [Fact]
    public void EveryEmittedRule_HasNonEmptyMessageAndFix()
    {
        // Run every rule through a profile that trips it, then assert the contract: every rule
        // ships with a non-empty Message + Fix (dashboard depends on both being present).
        EncodingProfile profile = ProfileFor(
            video: Video(width: 3840, height: 2160, level: "4.0", crf: 5, keyframeSeconds: 4),
            container: Container.HlsFmp4,
            segmentDurationSeconds: 6,
            audio: [Audio(AudioCodecType.Ac3, 333)],
            subtitles: [Subtitle(SubtitleCodecType.Ass, SubtitlePolicy.Extract)],
            ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(854, 480, VideoCodecType.H264, 4000, 4800, 8000, 24),
                    new(1920, 1080, VideoCodecType.H264, 2000, 2400, 4000, 24),
                ],
            },
            hdrPolicy: HdrPolicy.AlwaysPreserve
        );

        ValidationEnvelope env = ProfileRuleValidator.Validate(profile);
        foreach (EncoderRule rule in env.Errors.Concat(env.Warnings))
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Id), $"Rule has empty Id");
            Assert.False(string.IsNullOrWhiteSpace(rule.Field), $"Rule {rule.Id} has empty Field");
            Assert.False(
                string.IsNullOrWhiteSpace(rule.Message),
                $"Rule {rule.Id} has empty Message"
            );
            Assert.False(string.IsNullOrWhiteSpace(rule.Fix), $"Rule {rule.Id} has empty Fix");
        }
    }
}
