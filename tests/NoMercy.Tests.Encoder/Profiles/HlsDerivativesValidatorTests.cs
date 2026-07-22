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
/// Tests for the 4 HlsDerivatives validation rules added in Plan 4 Phase B.
/// </summary>
public class HlsDerivativesValidatorTests
{
    // ── Shared builders ───────────────────────────────────────────────────────

    private static EncodingProfile HlsProfile(
        Container container,
        HlsDerivatives? hlsDerivatives = null,
        StreamPolicy videoPolicy = StreamPolicy.Transcode
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "validator-test",
            Container: container,
            Video: videoPolicy == StreamPolicy.Omit
                ? null
                : new(
                    Policy: videoPolicy,
                    Codec: videoPolicy == StreamPolicy.Copy
                        ? VideoCodecType.Copy
                        : VideoCodecType.H264,
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
                ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: AllowedLanguages.All,
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "audio/{lang}-{codec}",
                    PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles: [],
            HlsDerivatives: hlsDerivatives
        );

    // ── Rule 1 — SpriteVtt requires HLS container ─────────────────────────────

    [Fact]
    public void Rule1_SpriteVtt_OnMkv_Rejects()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.Mkv,
            hlsDerivatives: new() { GenerateSpriteVtt = true }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(predicate: e => e.Contains("GenerateSpriteVtt") && e.Contains("HLS container"));
    }

    [Fact]
    public void Rule1_SpriteVtt_OnHlsFmp4_Passes()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { GenerateSpriteVtt = true }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.Errors.Should().NotContain(predicate: e => e.Contains("GenerateSpriteVtt"));
    }

    [Fact]
    public void Rule1_SpriteVtt_OnHlsTs_Passes()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsTs,
            hlsDerivatives: new() { GenerateSpriteVtt = true }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.Errors.Should().NotContain(predicate: e => e.Contains("GenerateSpriteVtt"));
    }

    [Fact]
    public void Rule1_SpriteVtt_False_OnMkv_Passes()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.Mkv,
            hlsDerivatives: new() { GenerateSpriteVtt = false }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.Errors.Should().NotContain(predicate: e => e.Contains("GenerateSpriteVtt"));
    }

    // ── Rule 2 — IFramePlaylists + Copy video → reject ────────────────────────

    [Fact]
    public void Rule2_IFramePlaylists_WithCopyVideo_Rejects()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { GenerateIFramePlaylists = true },
            videoPolicy: StreamPolicy.Copy
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(predicate: e => e.Contains("GenerateIFramePlaylists") && e.Contains("Copy"));
    }

    [Fact]
    public void Rule2_IFramePlaylists_WithTranscodeVideo_Passes()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { GenerateIFramePlaylists = true },
            videoPolicy: StreamPolicy.Transcode
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.Errors.Should().NotContain(predicate: e => e.Contains("GenerateIFramePlaylists"));
    }

    // ── Rule 3 — GenerateMasterPlaylist = false on HLS → warn (not reject) ────

    [Fact]
    public void Rule3_MasterPlaylistFalse_OnHls_EmitsWarning()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { GenerateMasterPlaylist = false }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.IsValid.Should().BeTrue();
        result
            .Warnings.Should()
            .Contain(predicate: w => w.Contains("GenerateMasterPlaylist") && w.Contains("false"));
    }

    [Fact]
    public void Rule3_MasterPlaylistFalse_OnNonHls_NoWarning()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.Mkv,
            hlsDerivatives: new()
            {
                GenerateMasterPlaylist = false,
                GenerateSpriteVtt = false,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.Warnings.Should().NotContain(predicate: w => w.Contains("GenerateMasterPlaylist"));
    }

    // ── Rule 4 — SpriteVtt numeric ranges ─────────────────────────────────────

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: -1)]
    [InlineData(data: 601)]
    public void Rule4_SpriteVttIntervalSeconds_OutOfRange_Rejects(int value)
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { SpriteVttIntervalSeconds = value }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(predicate: e => e.Contains("SpriteVttIntervalSeconds"));
    }

    [Fact]
    public void Rule4_SpriteVttIntervalSeconds_Valid_Passes()
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { SpriteVttIntervalSeconds = 10 }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.Errors.Should().NotContain(predicate: e => e.Contains("SpriteVttIntervalSeconds"));
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: -1)]
    [InlineData(data: 21)]
    public void Rule4_SpriteVttColumns_OutOfRange_Rejects(int value)
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { SpriteVttColumns = value }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(predicate: e => e.Contains("SpriteVttColumns"));
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: -1)]
    [InlineData(data: 21)]
    public void Rule4_SpriteVttRows_OutOfRange_Rejects(int value)
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { SpriteVttRows = value }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(predicate: e => e.Contains("SpriteVttRows"));
    }

    [Theory]
    [InlineData(data: 0)]
    [InlineData(data: -1)]
    [InlineData(data: 1921)]
    public void Rule4_SpriteVttThumbnailWidth_OutOfRange_Rejects(int value)
    {
        EncodingProfile profile = HlsProfile(
            container: Container.HlsFmp4,
            hlsDerivatives: new() { SpriteVttThumbnailWidth = value }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(predicate: e => e.Contains("SpriteVttThumbnailWidth"));
    }

    // ── Rule 4 happy-path: all defaults pass ─────────────────────────────────

    [Fact]
    public void Rule4_DefaultValues_AllPass()
    {
        EncodingProfile profile = HlsProfile(container: Container.HlsFmp4, hlsDerivatives: new());

        ProfileValidationResult result = ProfileValidator.Validate(profile: profile);

        result
            .Errors.Should()
            .NotContain(predicate: e =>
                e.Contains("SpriteVtt")
                && (
                    e.Contains("IntervalSeconds")
                    || e.Contains("Columns")
                    || e.Contains("Rows")
                    || e.Contains("Width")
                )
            );
    }
}
