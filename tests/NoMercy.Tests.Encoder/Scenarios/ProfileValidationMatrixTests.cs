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
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            Width: width,
            Height: height,
            RateControl: rc,
            Crf: crf,
            BitrateKbps: bitrate,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "fast",
            CodecProfile: CodecProfile.Auto,
            Level: level,
            Tune: null,
            BitDepth: bitDepth,
            PixelFormat: pixelFormat,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/{label}",
            PlaylistNameTemplate: "video/{label}/playlist"
        );

    private static AudioOutput Audio(
        AudioCodecType codec = AudioCodecType.Aac,
        int bitrate = 192
    ) =>
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
            SegmentNameTemplate: "audio/{lang}-{codec}",
            PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
        );

    private static SubtitleOutput Subtitle(SubtitleCodecType codec = SubtitleCodecType.Ass) =>
        new(Policy: SubtitlePolicy.Extract, Codec: codec, AllowedLanguages: ["eng"], IncludeForced: true, OcrLanguage: null, PlaylistNameTemplate: "subs/{lang}");

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
        new(Id: Ulid.NewUlid(), Name: name, Container: container, Video: video, Audio: audio ?? [], Subtitles: subtitles ?? [], Thumbnails: null, Ladder: ladder)
        {
            HdrPolicies = hdr,
            CustomArguments = customArgs,
        };

    private static bool HasRule(ValidationEnvelope env, string id) =>
        env.Errors.Any(predicate: r => r.Id == id) || env.Warnings.Any(predicate: r => r.Id == id);

    private static EncoderRule? FindRule(ValidationEnvelope env, string id) =>
        env.Errors.FirstOrDefault(predicate: r => r.Id == id) ?? env.Warnings.FirstOrDefault(predicate: r => r.Id == id);

    [Fact]
    public void H265InHlsTs_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(container: Container.HlsTs, video: Video(codec: VideoCodecType.H265))
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.HlsFmp4CodecMismatch).Should().BeTrue();
        FindRule(env: result, id: EncoderRuleId.HlsFmp4CodecMismatch)!.Fix.Should().Contain(expected: "HlsFmp4");
    }

    [Fact]
    public void Vp9InMp4_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(container: Container.Mp4, video: Video(codec: VideoCodecType.Vp9))
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.CodecContainerMismatch).Should().BeTrue();
    }

    [Fact]
    public void TruehdInMp4_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(
                container: Container.Mp4,
                audio: [Audio(codec: AudioCodecType.TrueHd, bitrate: 0)]
            )
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.AudioCodecContainerMismatch).Should().BeTrue();
    }

    [Fact]
    public void CrfModeNoCrf_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(rc: RateControlMode.Crf, crf: 0))
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.VideoRateControlMissing).Should().BeTrue();
    }

    [Fact]
    public void VbrModeNoBitrate_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(rc: RateControlMode.Vbr, bitrate: 0))
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.VideoRateControlMissing).Should().BeTrue();
    }

    [Fact]
    public void VbrWithCrfButNoBitrate_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(rc: RateControlMode.Vbr, bitrate: 0, crf: 23))
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.VideoRateControlConflict).Should().BeTrue();
    }

    [Fact]
    public void AacWithZeroBitrate_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(audio: [Audio(codec: AudioCodecType.Aac, bitrate: 0)])
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.AudioBitrateMissing).Should().BeTrue();
    }

    [Fact]
    public void ManualLadderNoRungs_Rejected()
    {
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = [] };
        ValidationEnvelope result = ProfileRuleValidator.Validate(profile: Profile(ladder: ladder));
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.LadderManualEmpty).Should().BeTrue();
    }

    [Fact]
    public void ManualLadderUnsorted_Rejected()
    {
        LadderRung[] rungs =
        [
            new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 5000, MaxBitrateKbps: 7500, BufferSizeKbps: 10000, Framerate: 30),
            new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 3000, MaxBitrateKbps: 4500, BufferSizeKbps: 6000, Framerate: 30),
        ];
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = rungs };
        ValidationEnvelope result = ProfileRuleValidator.Validate(profile: Profile(ladder: ladder));
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.LadderManualUnsorted).Should().BeTrue();
    }

    [Fact]
    public void Level4K_4_0_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(
                video: Video(codec: VideoCodecType.H264, width: 3840, height: 2160, level: "4.0")
            )
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.LevelResolutionMismatch).Should().BeTrue();
    }

    [Fact]
    public void Bitrate1080At200kbps_Warning()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(
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
        HasRule(env: result, id: EncoderRuleId.BitrateTooLowForResolution).Should().BeTrue();
        FindRule(env: result, id: EncoderRuleId.BitrateTooLowForResolution)!
            .Severity.Should()
            .Be(expected: EncoderRuleSeverity.Warning);
    }

    [Fact]
    public void CrfOutOfRange_Warning()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(rc: RateControlMode.Crf, crf: 50))
        );
        HasRule(env: result, id: EncoderRuleId.CrfOutOfTypicalRange).Should().BeTrue();
        FindRule(env: result, id: EncoderRuleId.CrfOutOfTypicalRange)!
            .Severity.Should()
            .Be(expected: EncoderRuleSeverity.Warning);
    }

    [Fact]
    public void ProfileNameMissing_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(profile: Profile(name: ""));
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.ProfileNameMissing).Should().BeTrue();
    }

    [Fact]
    public void VideoWidthZero_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(profile: Profile(video: Video(width: 0)));
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.VideoWidthInvalid).Should().BeTrue();
    }

    [Fact]
    public void VideoHeightNegative_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(height: -1))
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.VideoHeightInvalid).Should().BeTrue();
    }

    [Fact]
    public void DuplicateLadderRung_Warning()
    {
        LadderRung[] rungs =
        [
            new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 5000, MaxBitrateKbps: 7500, BufferSizeKbps: 10000, Framerate: 30),
            new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 5000, MaxBitrateKbps: 7500, BufferSizeKbps: 10000, Framerate: 30),
        ];
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = rungs };
        ValidationEnvelope result = ProfileRuleValidator.Validate(profile: Profile(ladder: ladder));
        HasRule(env: result, id: EncoderRuleId.LadderDuplicateVariant).Should().BeTrue();
        FindRule(env: result, id: EncoderRuleId.LadderDuplicateVariant)!
            .Severity.Should()
            .Be(expected: EncoderRuleSeverity.Warning);
    }

    [Fact]
    public void CustomArgsReservedFlag_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(), customArgs: new() { { "-c:v", "libx264" } })
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.CustomArgsReservedFlag).Should().BeTrue();
    }

    [Fact]
    public void AssInMp4_Rejected()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(container: Container.Mp4, subtitles: [Subtitle(codec: SubtitleCodecType.Ass)])
        );
        result.Valid.Should().BeFalse();
        HasRule(env: result, id: EncoderRuleId.SubtitlesContainerIncompatible).Should().BeTrue();
    }

    [Fact]
    public void H264InHlsTs_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(
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
            profile: Profile(
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
            profile: Profile(
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
            profile: Profile(
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
            profile: Profile(
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
            profile: Profile(video: Video(width: 1920, height: 1080, rc: RateControlMode.Vbr, bitrate: 5000))
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ManualLadderSorted_Valid()
    {
        LadderRung[] rungs =
        [
            new(Width: 640, Height: 360, Codec: VideoCodecType.H264, BitrateKbps: 500, MaxBitrateKbps: 750, BufferSizeKbps: 1000, Framerate: 30),
            new(Width: 1280, Height: 720, Codec: VideoCodecType.H264, BitrateKbps: 2000, MaxBitrateKbps: 3000, BufferSizeKbps: 4000, Framerate: 30),
            new(Width: 1920, Height: 1080, Codec: VideoCodecType.H264, BitrateKbps: 5000, MaxBitrateKbps: 7500, BufferSizeKbps: 10000, Framerate: 30),
        ];
        LadderConfig ladder = new() { Mode = LadderMode.Manual, Rungs = rungs };
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(), ladder: ladder)
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void OneEightyAt4_1_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(width: 1920, height: 1080, level: "4.1"))
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CrfInRange_Valid()
    {
        ValidationEnvelope result = ProfileRuleValidator.Validate(
            profile: Profile(video: Video(rc: RateControlMode.Crf, crf: 23))
        );
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
