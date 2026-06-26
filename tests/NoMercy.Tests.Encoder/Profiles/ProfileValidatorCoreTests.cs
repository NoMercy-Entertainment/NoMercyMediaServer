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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// Covers the core <see cref="ProfileValidator"/> branches that aren't owned by the
/// dedicated rule-set test files (AutoLadder / HfrStereoVr / HlsDerivatives /
/// SubtitleAcquisition). Each test category pins a regression we've actually
/// seen or want to prevent:
///
/// • Container × codec compatibility — wrong codec sneaking into HlsTs / Mp4 / audio-only
///   containers is the #1 way a profile crashes ffmpeg at mux time. Validator must reject
///   pre-encode so the error surface is "your profile is invalid" not "ffmpeg died".
/// • Audio bitrate guard — 0/-1 kbps for lossy codecs silently produces broken output;
///   FLAC and TrueHD are lossless and ignore bitrate, must NOT trip the rule.
/// • Manual ladder shape — empty rungs and out-of-order rungs are operator typos
///   that must surface immediately, not during planning.
/// • CMAF compatibility — only triggers when HLS-fMP4 + CmafCompatible=true; gating
///   logic must short-circuit cleanly for non-HLS / CMAF-off / Copy-policy cases.
/// • Forbidden custom-arg warnings — c:v/c:a/c:s/f/vcodec/etc. override container/codec
///   intent; we warn now and intend to hard-reject later.
/// • Orchestration — Validate aggregates errors from every sub-validator; IsValid
///   reflects errors only (warnings don't fail validity).
/// </summary>
public class ProfileValidatorCoreTests
{
    // ── Shared builders ───────────────────────────────────────────────────────

    private static VideoOutput Video(
        VideoCodecType codec = VideoCodecType.H264,
        StreamPolicy policy = StreamPolicy.Transcode
    ) =>
        new(
            Policy: policy,
            Codec: policy == StreamPolicy.Copy ? VideoCodecType.Copy : codec,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 23,
            BitrateKbps: 0,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "fast",
            CodecProfile: CodecProfile.High,
            Level: "4.0",
            Tune: null,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            KeyframeIntervalSeconds: 4,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/{label}",
            PlaylistNameTemplate: "video/{label}/playlist"
        );

    private static AudioOutput Audio(
        AudioCodecType codec = AudioCodecType.Aac,
        int bitrateKbps = 192,
        StreamPolicy policy = StreamPolicy.Transcode
    ) =>
        new(
            Policy: policy,
            Codec: policy == StreamPolicy.Copy ? AudioCodecType.Copy : codec,
            BitrateKbps: bitrateKbps,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: AllowedLanguages.All,
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio/{lang}-{codec}",
            PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
        );

    private static EncodingProfile Profile(
        Container container = Container.Mkv,
        VideoOutput? video = null,
        AudioOutput[]? audio = null,
        LadderConfig? ladder = null,
        HlsConfig? hls = null,
        Dictionary<string, string>? customArguments = null
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "core-validator-test",
            Container: container,
            Video: video ?? Video(),
            Audio: audio ?? [Audio()],
            Subtitles: [],
            Ladder: ladder,
            Hls: hls
        )
        {
            CustomArguments = customArguments,
        };

    // ── ValidateContainerCompatibility ────────────────────────────────────────

    [Fact]
    public void Video_codec_incompatible_with_container_rejects()
    {
        // HlsTs only supports H264 — H265 must be rejected with a suggestion list.
        EncodingProfile profile = Profile(
            container: Container.HlsTs,
            video: Video(codec: VideoCodecType.H265)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Contains("HlsTs") && e.Contains("H265") && e.Contains("Compatible containers")
            );
    }

    [Fact]
    public void Video_codec_compatible_with_container_passes()
    {
        // HlsFmp4 supports all three modern video codecs — Av1 must not trigger the rule.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            video: Video(codec: VideoCodecType.Av1)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("does not support video"));
    }

    [Fact]
    public void Video_codec_check_skipped_when_policy_is_copy()
    {
        // Copy never re-muxes through a codec path, so container compatibility doesn't apply.
        EncodingProfile profile = Profile(
            container: Container.HlsTs,
            video: Video(policy: StreamPolicy.Copy)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("does not support video"));
    }

    [Fact]
    public void Audio_codec_incompatible_with_container_rejects()
    {
        // HlsFmp4 only supports Aac/Ac3/Eac3 — Opus must be rejected.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            audio: [Audio(codec: AudioCodecType.Opus)]
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Contains("HlsFmp4") && e.Contains("Opus") && e.Contains("Compatible containers")
            );
    }

    [Fact]
    public void Audio_codec_check_skipped_when_policy_is_copy()
    {
        // Copy audio bypasses container codec check just like video.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            audio: [Audio(codec: AudioCodecType.Opus, policy: StreamPolicy.Copy)]
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("does not support audio"));
    }

    [Fact]
    public void Multiple_audio_outputs_each_checked_independently()
    {
        // First track Aac (legal in Mp4), second track Opus (illegal in Mp4) — only the
        // second should produce an error; the loop must not bail on first failure.
        EncodingProfile profile = Profile(
            container: Container.Mp4,
            audio: [Audio(codec: AudioCodecType.Aac), Audio(codec: AudioCodecType.Opus)]
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Mp4") && e.Contains("Opus"));
        result.Errors.Should().NotContain(e => e.Contains("Mp4") && e.Contains("Aac"));
    }

    // ── ValidateAudioBitrate ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-128)]
    public void Audio_transcode_with_non_positive_bitrate_rejects(int bitrate)
    {
        EncodingProfile profile = Profile(audio: [Audio(bitrateKbps: bitrate)]);

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("BitrateKbps must be > 0"));
    }

    [Fact]
    public void Audio_transcode_with_positive_bitrate_passes()
    {
        EncodingProfile profile = Profile(audio: [Audio(bitrateKbps: 1)]);

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("BitrateKbps must be > 0"));
    }

    [Fact]
    public void Audio_bitrate_check_skipped_for_flac()
    {
        // FLAC is lossless — BitrateKbps is meaningless, must not trip the rule.
        EncodingProfile profile = Profile(
            container: Container.Mkv,
            audio: [Audio(codec: AudioCodecType.Flac, bitrateKbps: 0)]
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("BitrateKbps must be > 0"));
    }

    [Fact]
    public void Audio_bitrate_check_skipped_for_truehd()
    {
        // TrueHD is lossless — same exception as FLAC.
        EncodingProfile profile = Profile(
            container: Container.Mkv,
            audio: [Audio(codec: AudioCodecType.TrueHd, bitrateKbps: 0)]
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("BitrateKbps must be > 0"));
    }

    [Fact]
    public void Audio_bitrate_check_skipped_for_copy_policy()
    {
        // Copy doesn't re-encode — BitrateKbps is informational only.
        EncodingProfile profile = Profile(
            audio: [Audio(bitrateKbps: 0, policy: StreamPolicy.Copy)]
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("BitrateKbps must be > 0"));
    }

    // ── ValidateLadder (Manual mode) ─────────────────────────────────────────

    [Fact]
    public void Manual_ladder_with_null_rungs_rejects()
    {
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            ladder: new LadderConfig { Mode = LadderMode.Manual, Rungs = null }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Manual ladder requires non-empty Rungs"));
    }

    [Fact]
    public void Manual_ladder_with_empty_rungs_rejects()
    {
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            ladder: new LadderConfig { Mode = LadderMode.Manual, Rungs = [] }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Manual ladder requires non-empty Rungs"));
    }

    [Fact]
    public void Manual_ladder_with_unsorted_rungs_rejects()
    {
        // Second rung has LOWER bitrate than first — ABR clients require ascending order.
        LadderRung[] rungs =
        [
            new(
                Width: 1920,
                Height: 1080,
                Codec: VideoCodecType.H264,
                BitrateKbps: 5000,
                MaxBitrateKbps: 7500,
                BufferSizeKbps: 10000,
                Framerate: 30
            ),
            new(
                Width: 1280,
                Height: 720,
                Codec: VideoCodecType.H264,
                BitrateKbps: 3000,
                MaxBitrateKbps: 4500,
                BufferSizeKbps: 6000,
                Framerate: 30
            ),
        ];

        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            ladder: new LadderConfig { Mode = LadderMode.Manual, Rungs = rungs }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("Manual ladder rungs must be sorted ascending by bitrate"));
    }

    [Fact]
    public void Manual_ladder_with_equal_bitrates_rejects()
    {
        // Equal bitrates are also rejected — the predicate is strict ascending (>).
        LadderRung[] rungs =
        [
            new(
                Width: 1280,
                Height: 720,
                Codec: VideoCodecType.H264,
                BitrateKbps: 3000,
                MaxBitrateKbps: 4500,
                BufferSizeKbps: 6000,
                Framerate: 30
            ),
            new(
                Width: 1920,
                Height: 1080,
                Codec: VideoCodecType.H264,
                BitrateKbps: 3000,
                MaxBitrateKbps: 4500,
                BufferSizeKbps: 6000,
                Framerate: 30
            ),
        ];

        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            ladder: new LadderConfig { Mode = LadderMode.Manual, Rungs = rungs }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result
            .Errors.Should()
            .Contain(e => e.Contains("Manual ladder rungs must be sorted ascending by bitrate"));
    }

    [Fact]
    public void Manual_ladder_with_ascending_rungs_passes()
    {
        LadderRung[] rungs =
        [
            new(
                Width: 1280,
                Height: 720,
                Codec: VideoCodecType.H264,
                BitrateKbps: 3000,
                MaxBitrateKbps: 4500,
                BufferSizeKbps: 6000,
                Framerate: 30
            ),
            new(
                Width: 1920,
                Height: 1080,
                Codec: VideoCodecType.H264,
                BitrateKbps: 5000,
                MaxBitrateKbps: 7500,
                BufferSizeKbps: 10000,
                Framerate: 30
            ),
        ];

        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            ladder: new LadderConfig { Mode = LadderMode.Manual, Rungs = rungs }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("Manual ladder"));
    }

    [Fact]
    public void Null_ladder_skipped_entirely()
    {
        EncodingProfile profile = Profile(container: Container.HlsFmp4, ladder: null);

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("ladder") || e.Contains("Ladder"));
    }

    // ── ValidateCmafCompatibility ────────────────────────────────────────────
    //
    // Note: the CMAF *video* error path (ContainerCompatibility.IsCmafCompatible
    // returning false) is unreachable through public API today — HlsFmp4's video
    // matrix is {H264, H265, Av1}, which is exactly the CMAF-allowed set, so
    // container-compat rejects any non-CMAF video before the CMAF check runs.
    // CMAF *audio* IS reachable because HlsFmp4 allows {Aac, Ac3, Eac3} but CMAF
    // only allows {Aac, Eac3} — Ac3 lives in the gap.

    [Fact]
    public void Cmaf_with_incompatible_audio_codec_rejects()
    {
        // HlsFmp4 audio matrix is {Aac, Ac3, Eac3}. CMAF audio is {Aac, Eac3}.
        // Ac3 is container-legal but NOT CMAF-compatible — perfect intersection
        // for proving the Cmaf branch fires.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            video: Video(codec: VideoCodecType.H264),
            audio: [Audio(codec: AudioCodecType.Ac3)],
            hls: new HlsConfig(CmafCompatible: true)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("CMAF requires a CMAF-compatible audio codec"));
    }

    [Fact]
    public void Cmaf_check_skipped_when_cmaf_compatible_false()
    {
        // Same Ac3 audio, but CmafCompatible=false — the rule must NOT trigger.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            video: Video(codec: VideoCodecType.H264),
            audio: [Audio(codec: AudioCodecType.Ac3)],
            hls: new HlsConfig(CmafCompatible: false)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("CMAF requires"));
    }

    [Fact]
    public void Cmaf_check_skipped_when_container_not_hls_fmp4()
    {
        // CmafCompatible=true but container is HlsTs — branch must short-circuit.
        EncodingProfile profile = Profile(
            container: Container.HlsTs,
            video: Video(codec: VideoCodecType.H264),
            audio: [Audio(codec: AudioCodecType.Ac3)],
            hls: new HlsConfig(CmafCompatible: true)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("CMAF requires"));
    }

    [Fact]
    public void Cmaf_check_skipped_when_hls_config_null()
    {
        // No HlsConfig at all — Cmaf gate is null-safe.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            video: Video(codec: VideoCodecType.H264),
            audio: [Audio(codec: AudioCodecType.Aac)],
            hls: null
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("CMAF requires"));
    }

    [Fact]
    public void Cmaf_audio_check_skipped_when_audio_policy_is_copy()
    {
        // Copy audio bypasses CMAF compat just like container compat.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            video: Video(codec: VideoCodecType.H264),
            audio: [Audio(codec: AudioCodecType.Ac3, policy: StreamPolicy.Copy)],
            hls: new HlsConfig(CmafCompatible: true)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("CMAF requires"));
    }

    [Fact]
    public void Cmaf_passes_with_aac_and_h264()
    {
        // Canonical CMAF combo: H264 video + Aac audio in HlsFmp4 with CmafCompatible=true.
        EncodingProfile profile = Profile(
            container: Container.HlsFmp4,
            video: Video(codec: VideoCodecType.H264),
            audio: [Audio(codec: AudioCodecType.Aac)],
            hls: new HlsConfig(CmafCompatible: true)
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("CMAF requires"));
    }

    // ── ValidateCustomArguments ──────────────────────────────────────────────

    [Theory]
    [InlineData("c:v")]
    [InlineData("c:a")]
    [InlineData("c:s")]
    [InlineData("f")]
    [InlineData("vcodec")]
    [InlineData("acodec")]
    [InlineData("scodec")]
    public void Forbidden_custom_arg_produces_warning(string key)
    {
        EncodingProfile profile = Profile(
            customArguments: new Dictionary<string, string> { [key] = "libx264" }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        // Warnings don't make IsValid false — they're informational.
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains(key) && w.Contains("hard-reject"));
    }

    [Theory]
    [InlineData("C:V")]
    [InlineData("Vcodec")]
    [InlineData("ACODEC")]
    public void Forbidden_custom_arg_match_is_case_insensitive(string key)
    {
        // The hash set uses StringComparer.OrdinalIgnoreCase — casing must not bypass.
        EncodingProfile profile = Profile(
            customArguments: new Dictionary<string, string> { [key] = "anything" }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Warnings.Should().Contain(w => w.Contains(key));
    }

    [Fact]
    public void Non_forbidden_custom_arg_produces_no_warning()
    {
        EncodingProfile profile = Profile(
            customArguments: new Dictionary<string, string>
            {
                ["preset"] = "slow",
                ["crf"] = "20",
                ["g"] = "120",
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Warnings.Should().NotContain(w => w.Contains("hard-reject"));
    }

    [Fact]
    public void Null_custom_arguments_handled_safely()
    {
        EncodingProfile profile = Profile(customArguments: null);

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Warnings.Should().NotContain(w => w.Contains("hard-reject"));
    }

    [Fact]
    public void Multiple_forbidden_args_each_produce_separate_warning()
    {
        // Two forbidden keys must each emit a warning — loop must not deduplicate.
        EncodingProfile profile = Profile(
            customArguments: new Dictionary<string, string> { ["c:v"] = "libx264", ["c:a"] = "aac" }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Warnings.Should().Contain(w => w.Contains("c:v"));
        result.Warnings.Should().Contain(w => w.Contains("c:a"));
        result.Warnings.Count(w => w.Contains("hard-reject")).Should().Be(2);
    }

    // ── Validate orchestration ───────────────────────────────────────────────

    [Fact]
    public void Validate_aggregates_errors_from_multiple_sub_validators()
    {
        // One profile that breaks container compat AND audio bitrate — both errors must surface.
        EncodingProfile profile = Profile(
            container: Container.HlsTs,
            video: Video(codec: VideoCodecType.H265), // container error
            audio: [Audio(bitrateKbps: 0)] // bitrate error
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("does not support video"));
        result.Errors.Should().Contain(e => e.Contains("BitrateKbps must be > 0"));
    }

    [Fact]
    public void Validate_is_valid_when_only_warnings_present()
    {
        // Forbidden custom arg is the only issue — IsValid should still be true.
        EncodingProfile profile = Profile(
            customArguments: new Dictionary<string, string> { ["c:v"] = "libx264" }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void Valid_profile_returns_no_errors_or_warnings()
    {
        // Vanilla Mkv + H264 + Aac@192 — nothing should trip.
        EncodingProfile profile = Profile();

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }
}
