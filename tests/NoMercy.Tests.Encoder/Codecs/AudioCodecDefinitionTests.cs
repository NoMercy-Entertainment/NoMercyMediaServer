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
using NoMercy.Encoder.Codecs.Definitions;

namespace NoMercy.Tests.Encoder.Codecs;

public class AudioCodecDefinitionTests
{
    [Fact]
    public void Aac_HasCorrectEncoder()
    {
        AudioEncoderInfo aac = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Aac);
        aac.FfmpegName.Should().Be(expected: "libfdk_aac");
        aac.Channels.Should().Contain(expected: 2);
        aac.Channels.Should().Contain(expected: 6);
    }

    [Fact]
    public void Flac_IsLossless()
    {
        AudioEncoderInfo flac = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Flac);
        flac.FfmpegName.Should().Be(expected: "flac");
        flac.IsLossless.Should().BeTrue();
    }

    [Fact]
    public void Opus_HasCorrectBitrateRange()
    {
        AudioEncoderInfo opus = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Opus);
        opus.FfmpegName.Should().Be(expected: "libopus");
        opus.MinBitrateKbps.Should().Be(expected: 6);
        opus.MaxBitrateKbps.Should().Be(expected: 510);
    }

    [Fact]
    public void AllAudioTypes_HaveDefinitions()
    {
        foreach (AudioCodecType codecType in Enum.GetValues<AudioCodecType>())
        {
            AudioEncoderInfo encoder = AudioCodecDefinitions.GetEncoder(codecType: codecType);
            encoder.Should().NotBeNull();
            encoder.FfmpegName.Should().NotBeNullOrEmpty();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Channel-layout traps — most audio codecs don't support every channel
    // count. Getting these wrong means ffmpeg silently drops to stereo or
    // fails mid-encode with a cryptic "channel layout not supported" error.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ac3_DoesNotSupport71()
    {
        // AC3 tops out at 5.1 (6 channels). A profile asking for 7.1 AC3 must
        // be rejected by the validator — ffmpeg's ac3 encoder downmixes
        // silently when given 8 channels and the result is stereo or 5.1
        // depending on source layout, never 7.1.
        AudioEncoderInfo ac3 = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Ac3);
        ac3.Channels.Should().NotContain(unexpected: 8);
        ac3.Channels.Should().BeEquivalentTo(expectation: new[] { 1, 2, 6 });
    }

    [Fact]
    public void Dts_BaseCoreDoesNotSupport71()
    {
        // DTS core ("dca" in ffmpeg) is 5.1 max. DTS-HD MA / DTS:X extensions
        // support 7.1 but ffmpeg's dca encoder is core-only.
        AudioEncoderInfo dts = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Dts);
        dts.Channels.Should().NotContain(unexpected: 8);
    }

    [Fact]
    public void Eac3_Supports71()
    {
        // E-AC3 is the step up from AC3 specifically to add 7.1 support.
        // DASH/HLS Dolby Digital Plus output relies on this.
        AudioEncoderInfo eac3 = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Eac3);
        eac3.Channels.Should().Contain(expected: 8);
    }

    [Fact]
    public void Mp3_StereoOnly()
    {
        // MP3 cannot carry surround. A profile requesting 5.1/7.1 MP3 is
        // broken on its face — the MP3 container validation layer relies
        // on this assertion.
        AudioEncoderInfo mp3 = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Mp3);
        mp3.Channels.Should().BeEquivalentTo(expectation: new[] { 1, 2 });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sample-rate constraints — Opus always resamples internally and rejects
    // any rate other than 48 kHz; AC3/DTS/EAC3 are 48 kHz only (no 44.1 or 96).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Opus_SampleRate_48kHzOnly()
    {
        AudioEncoderInfo opus = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Opus);
        opus.SampleRates.Should().BeEquivalentTo(expectation: new[] { 48000 });
    }

    [Fact]
    public void Ac3AndEac3AndDts_48kHzOnly()
    {
        AudioCodecType[] dolbyDts = [AudioCodecType.Ac3, AudioCodecType.Eac3, AudioCodecType.Dts];
        foreach (AudioCodecType codec in dolbyDts)
        {
            AudioEncoderInfo encoder = AudioCodecDefinitions.GetEncoder(codecType: codec);
            encoder
                .SampleRates.Should()
                .BeEquivalentTo(expectation: new[] { 48000 }, because: $"{codec} is 48 kHz only by spec");
        }
    }

    [Fact]
    public void Flac_SupportsUpTo192kHz()
    {
        // FLAC is the archival codec — must keep source sample rates
        // verbatim, up to 192 kHz for high-res audio sources.
        AudioEncoderInfo flac = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Flac);
        flac.SampleRates.Should().Contain(expected: 192000);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Lossless gating — validator uses IsLossless to skip the BitrateKbps > 0
    // check. Getting IsLossless wrong means lossless codecs either demand a
    // non-zero bitrate (broken) or lossy codecs accept zero (broken).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LosslessCodecs_ReportZeroBitrateRange()
    {
        AudioCodecType[] lossless = [AudioCodecType.Flac, AudioCodecType.TrueHd];
        foreach (AudioCodecType codec in lossless)
        {
            AudioEncoderInfo encoder = AudioCodecDefinitions.GetEncoder(codecType: codec);
            encoder.IsLossless.Should().BeTrue();
            encoder.MinBitrateKbps.Should().Be(expected: 0);
            encoder.MaxBitrateKbps.Should().Be(expected: 0);
            encoder.DefaultBitrateKbps.Should().Be(expected: 0);
        }
    }

    [Fact]
    public void LossyCodecs_HaveValidBitrateRange()
    {
        AudioCodecType[] lossy =
        [
            AudioCodecType.Aac,
            AudioCodecType.Opus,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
            AudioCodecType.Vorbis,
            AudioCodecType.Dts,
        ];
        foreach (AudioCodecType codec in lossy)
        {
            AudioEncoderInfo encoder = AudioCodecDefinitions.GetEncoder(codecType: codec);
            encoder.IsLossless.Should().BeFalse();
            encoder.MinBitrateKbps.Should().BeGreaterThan(expected: 0);
            encoder.MaxBitrateKbps.Should().BeGreaterThan(expected: encoder.MinBitrateKbps);
            encoder
                .DefaultBitrateKbps.Should()
                .BeInRange(minimumValue: encoder.MinBitrateKbps, maximumValue: encoder.MaxBitrateKbps);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FFmpeg encoder name pinning — changing these silently breaks every
    // encode, so keep them locked down. libfdk_aac requires --enable-nonfree.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Aac_UsesLibFdkAac_NotBuiltInAac()
    {
        // nomercy-ffmpeg builds with --enable-nonfree to enable libfdk_aac —
        // the stock "aac" encoder quality is noticeably worse at matched
        // bitrate. If this ever regresses to "aac" the build is misconfigured.
        AudioEncoderInfo aac = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Aac);
        aac.FfmpegName.Should().Be(expected: "libfdk_aac");
    }

    [Fact]
    public void Dts_UsesDca_NotDts()
    {
        // ffmpeg encoder is named "dca" (per the DCA Coherent Acoustics spec);
        // the "dts" name is only on the decoder side.
        AudioEncoderInfo dts = AudioCodecDefinitions.GetEncoder(codecType: AudioCodecType.Dts);
        dts.FfmpegName.Should().Be(expected: "dca");
    }
}
