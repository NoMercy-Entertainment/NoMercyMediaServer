// SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles;

public class ProfileValidationMatrixTests
{
    private static VideoOutput Video(
        VideoCodecType codec = VideoCodecType.H264,
        int? width = 1920,
        int? height = 1080,
        RateControlMode rc = RateControlMode.Crf,
        int crf = 23,
        int bitrate = 0,
        string? level = null,
        int bitDepth = 8,
        string? pixelFormat = null
    ) =>
        new(
            StreamPolicy.Transcode,
            codec,
            width,
            height,
            rc,
            crf,
            bitrate,
            null,
            null,
            "fast",
            CodecProfile.Auto,
            level,
            null,
            bitDepth,
            pixelFormat,
            2,
            false,
            "video/{label}",
            "video/{label}/playlist"
        );

    private static AudioOutput Audio(
        AudioCodecType codec = AudioCodecType.Aac,
        int bitrate = 192
    ) =>
        new(
            StreamPolicy.Transcode,
            codec,
            bitrate,
            2,
            48000,
            ["eng"],
            "eng",
            null,
            null,
            "audio/{lang}-{codec}",
            "audio/{lang}-{codec}/playlist"
        );

    private static SubtitleOutput Subtitle(SubtitleCodecType codec = SubtitleCodecType.Ass) =>
        new(SubtitlePolicy.Extract, codec, ["eng"], true, null, "subs/{lang}");

    private static EncodingProfile Profile(
        string name = "test",
        Container container = Container.HlsFmp4,
        VideoOutput? video = null,
        AudioOutput[]? audio = null,
        SubtitleOutput[]? subtitles = null,
        LadderConfig? ladder = null,
        HdrPolicies hdr = HdrPolicies.PassthroughWhenPossible,
        Dictionary<string, string>? customArgs = null
    ) =>
        new(Ulid.NewUlid(), name, container, video, audio ?? [], subtitles ?? [], null, ladder)
        {
            HdrPolicies = hdr,
            CustomArguments = customArgs,
        };

    private static bool HasRule(ValidationEnvelope env, string id) =>
        env.Errors.Any(r => r.Id == id) || env.Warnings.Any(r => r.Id == id);

    private static EncoderRule? FindRule(ValidationEnvelope env, string id) =>
        env.Errors.FirstOrDefault(r => r.Id == id) ?? env.Warnings.FirstOrDefault(r => r.Id == id);

    [Fact]
    public void H265InHlsTs_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(container: Container.HlsTs, video: Video(codec: VideoCodecType.H265))
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.HlsFmp4CodecMismatch).Should().BeTrue();
        FindRule(result, EncoderRuleId.HlsFmp4CodecMismatch)!.Fix.Should().Contain("HlsFmp4");
    }

    [Fact]
    public void Vp9InMp4_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(container: Container.Mp4, video: Video(codec: VideoCodecType.Vp9))
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.CodecContainerMismatch).Should().BeTrue();
    }

    [Fact]
    public void TruehdInMp4_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                container: Container.Mp4,
                audio: [Audio(codec: AudioCodecType.TrueHd, bitrate: 0)]
            )
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.AudioCodecContainerMismatch).Should().BeTrue();
    }

    [Fact]
    public void CrfModeNoCrf_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(rc: RateControlMode.Crf, crf: 0))
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.VideoRateControlMissing).Should().BeTrue();
    }

    [Fact]
    public void VbrModeNoBitrate_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(rc: RateControlMode.Vbr, bitrate: 0))
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.VideoRateControlMissing).Should().BeTrue();
    }

    [Fact]
    public void VbrWithCrfButNoBitrate_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(rc: RateControlMode.Vbr, bitrate: 0, crf: 23))
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.VideoRateControlConflict).Should().BeTrue();
    }

    [Fact]
    public void AacWithZeroBitrate_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(audio: [Audio(codec: AudioCodecType.Aac, bitrate: 0)])
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.AudioBitrateMissing).Should().BeTrue();
    }

    [Fact]
    public void ManualLadderNoRungs_Rejected()
    {
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = [] };
        ValidationEnvelope result = ProfileRuleValidator.Validate(Profile(ladder: ladder));
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.LadderManualEmpty).Should().BeTrue();
    }

    [Fact]
    public void ManualLadderUnsorted_Rejected()
    {
        LadderRung[] rungs =
        [
            new(1280, 720, VideoCodecType.H264, 5000, 7500, 10000, 30),
            new(1920, 1080, VideoCodecType.H264, 3000, 4500, 6000, 30),
        ];
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = rungs };
        ValidationEnvelope result = ProfileRuleValidator.Validate(Profile(ladder: ladder));
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.LadderManualUnsorted).Should().BeTrue();
    }

    [Fact]
    public void Level4K_4_0_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                video: Video(codec: VideoCodecType.H264, width: 3840, height: 2160, level: "4.0")
            )
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.LevelResolutionMismatch).Should().BeTrue();
    }

    [Fact]
    public void Bitrate1080At200kbps_Warning()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                video: Video(
                    codec: VideoCodecType.H264,
                    width: 1920,
                    height: 1080,
                    rc: RateControlMode.Vbr,
                    crf: 0,
                    bitrate: 200
                )
            )
        );
        HasRule(result, EncoderRuleId.BitrateTooLowForResolution).Should().BeTrue();
        FindRule(result, EncoderRuleId.BitrateTooLowForResolution)!
            .Severity.Should()
            .Be(EncoderRuleSeverity.Warning);
    }

    [Fact]
    public void CrfOutOfRange_Warning()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(rc: RateControlMode.Crf, crf: 50))
        );
        HasRule(result, EncoderRuleId.CrfOutOfTypicalRange).Should().BeTrue();
        FindRule(result, EncoderRuleId.CrfOutOfTypicalRange)!
            .Severity.Should()
            .Be(EncoderRuleSeverity.Warning);
    }

    [Fact]
    public void ProfileNameMissing_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(Profile(name: ""));
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.ProfileNameMissing).Should().BeTrue();
    }

    [Fact]
    public void VideoWidthZero_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(Profile(video: Video(width: 0)));
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.VideoWidthInvalid).Should().BeTrue();
    }

    [Fact]
    public void VideoHeightNegative_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(height: -1))
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.VideoHeightInvalid).Should().BeTrue();
    }

    [Fact]
    public void DuplicateLadderRung_Warning()
    {
        LadderRung[] rungs =
        [
            new(1920, 1080, VideoCodecType.H264, 5000, 7500, 10000, 30),
            new(1920, 1080, VideoCodecType.H264, 5000, 7500, 10000, 30),
        ];
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = rungs };
        ValidationEnvelope result = ProfileRuleValidator.Validate(Profile(ladder: ladder));
        HasRule(result, EncoderRuleId.LadderDuplicateVariant).Should().BeTrue();
        FindRule(result, EncoderRuleId.LadderDuplicateVariant)!
            .Severity.Should()
            .Be(EncoderRuleSeverity.Warning);
    }

    [Fact]
    public void CustomArgsReservedFlag_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(), customArgs: new() { { "-c:v", "libx264" } })
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.CustomArgsReservedFlag).Should().BeTrue();
    }

    [Fact]
    public void AssInMp4_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(container: Container.Mp4, subtitles: [Subtitle(codec: SubtitleCodecType.Ass)])
        );
        result.Valid.Should().BeFalse();
        HasRule(result, EncoderRuleId.SubtitlesContainerIncompatible).Should().BeTrue();
    }

    [Fact]
    public void H264InHlsTs_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                container: Container.HlsTs,
                video: Video(codec: VideoCodecType.H264),
                audio: [Audio(codec: AudioCodecType.Aac)]
            )
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void H265InHlsFmp4_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                container: Container.HlsFmp4,
                video: Video(codec: VideoCodecType.H265),
                audio: [Audio(codec: AudioCodecType.Aac)]
            )
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Av1InHlsFmp4_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                container: Container.HlsFmp4,
                video: Video(codec: VideoCodecType.Av1),
                audio: [Audio(codec: AudioCodecType.Aac)]
            )
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void H264Mp4WithAac_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                container: Container.Mp4,
                video: Video(codec: VideoCodecType.H264),
                audio: [Audio(codec: AudioCodecType.Aac, bitrate: 128)]
            )
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Hevc10BitArchive_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(
                container: Container.Mkv,
                video: Video(
                    codec: VideoCodecType.H265,
                    rc: RateControlMode.Crf,
                    crf: 18,
                    bitDepth: 10,
                    pixelFormat: "yuv420p10le"
                )
            )
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void OneTwentyAtFiveMbps_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(width: 1920, height: 1080, rc: RateControlMode.Vbr, bitrate: 5000))
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ManualLadderSorted_Valid()
    {
        LadderRung[] rungs =
        [
            new(640, 360, VideoCodecType.H264, 500, 750, 1000, 30),
            new(1280, 720, VideoCodecType.H264, 2000, 3000, 4000, 30),
            new(1920, 1080, VideoCodecType.H264, 5000, 7500, 10000, 30),
        ];
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = rungs };
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(), ladder: ladder)
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void OneEightyAt4_1_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(width: 1920, height: 1080, level: "4.1"))
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CrfInRange_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            Profile(video: Video(rc: RateControlMode.Crf, crf: 23))
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
