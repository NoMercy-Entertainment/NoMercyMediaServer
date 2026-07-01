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

using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Pipeline.Planners;

/// <summary>
/// AudioFilterBuilder constructs FFmpeg audio filter chains for downmixing (pan)
/// and loudness normalisation (loudnorm). Tests verify the exact filter output for
/// all combinations of LoudnessMode and DownmixMode.
/// </summary>
public class AudioFilterBuilderTests
{
    // ── Loudness only (no downmix) ──────────────────────────────────────────

    [Fact]
    public void BuildAudioFilter_LoudnessNone_ReturnsNull()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Auto,
            null
        );

        result.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_LoudnessEbuR128_ReturnsEbuR128Filter()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.EbuR128,
            DownmixMode.Auto,
            null
        );

        result.Should().Be("loudnorm=I=-16:TP=-1.5:LRA=11");
    }

    [Fact]
    public void BuildAudioFilter_LoudnessReplayGain_ReturnsReplayGainFilter()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.ReplayGain,
            DownmixMode.Auto,
            null
        );

        result.Should().Be("loudnorm=I=-18:TP=-1.5:LRA=11");
    }

    [Fact]
    public void BuildAudioFilter_LoudnessCustom_ReturnsNull()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.Custom,
            DownmixMode.Auto,
            null
        );

        result.Should().BeNull();
    }

    // ── Downmix only (no loudness) ──────────────────────────────────────────

    [Fact]
    public void BuildAudioFilter_DownmixAuto_ReturnsNull()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Auto,
            null
        );

        result.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_DownmixStereoItuR128_ReturnsPanFilter()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.StereoItuR128,
            null
        );

        result.Should()
            .Be("pan=stereo|FL<FL+0.707*FC+0.707*BL+0.707*SL|FR<FR+0.707*FC+0.707*BR+0.707*SR");
    }

    [Fact]
    public void BuildAudioFilter_DownmixMono_ReturnsPanFilter()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Mono,
            null
        );

        result.Should()
            .Be("pan=mono|c0<0.5*FL+0.5*FR+0.5*FC+0.25*BL+0.25*BR+0.25*SL+0.25*SR");
    }

    [Fact]
    public void BuildAudioFilter_DownmixCustomWithMatrix_ReturnsPanWithCustomMatrix()
    {
        string customMatrix = "stereo|FL<FL+0.5*FC|FR<FR+0.5*FC";
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Custom,
            customMatrix
        );

        result.Should().Be("pan=stereo|FL<FL+0.5*FC|FR<FR+0.5*FC");
    }

    [Fact]
    public void BuildAudioFilter_DownmixCustomWithoutMatrix_ReturnsNull()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Custom,
            null
        );

        result.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_DownmixCustomWithEmptyMatrix_ReturnsNull()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Custom,
            ""
        );

        result.Should().BeNull();
    }

    [Fact]
    public void BuildAudioFilter_DownmixCustomWithWhitespaceMatrix_ReturnsNull()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.None,
            DownmixMode.Custom,
            "   "
        );

        result.Should().BeNull();
    }

    // ── Combined: Downmix + Loudness ───────────────────────────────────────

    [Fact]
    public void BuildAudioFilter_StereoDownmixAndEbuR128_ChainsPanBeforeLoudnorm()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.EbuR128,
            DownmixMode.StereoItuR128,
            null
        );

        string expected =
            "pan=stereo|FL<FL+0.707*FC+0.707*BL+0.707*SL|FR<FR+0.707*FC+0.707*BR+0.707*SR,"
            + "loudnorm=I=-16:TP=-1.5:LRA=11";
        result.Should().Be(expected);
    }

    [Fact]
    public void BuildAudioFilter_MonoDownmixAndReplayGain_ChainsPanBeforeLoudnorm()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.ReplayGain,
            DownmixMode.Mono,
            null
        );

        string expected =
            "pan=mono|c0<0.5*FL+0.5*FR+0.5*FC+0.25*BL+0.25*BR+0.25*SL+0.25*SR,"
            + "loudnorm=I=-18:TP=-1.5:LRA=11";
        result.Should().Be(expected);
    }

    [Fact]
    public void BuildAudioFilter_CustomDownmixAndEbuR128_ChainsPanBeforeLoudnorm()
    {
        string customMatrix = "stereo|L<0.6*FL+0.4*FC|R<0.6*FR+0.4*FC";
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.EbuR128,
            DownmixMode.Custom,
            customMatrix
        );

        string expected = "pan=stereo|L<0.6*FL+0.4*FC|R<0.6*FR+0.4*FC,loudnorm=I=-16:TP=-1.5:LRA=11";
        result.Should().Be(expected);
    }

    [Fact]
    public void BuildAudioFilter_CustomDownmixWithoutMatrixAndLoudness_ReturnsLoudnormOnly()
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(
            LoudnessMode.EbuR128,
            DownmixMode.Custom,
            null
        );

        result.Should().Be("loudnorm=I=-16:TP=-1.5:LRA=11");
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LoudnessMode.None, DownmixMode.Auto, null)]
    [InlineData(LoudnessMode.Custom, DownmixMode.Auto, null)]
    [InlineData(LoudnessMode.None, DownmixMode.Custom, null)]
    [InlineData(LoudnessMode.Custom, DownmixMode.Custom, "")]
    public void BuildAudioFilter_AllNullCases_ReturnsNull(
        LoudnessMode loudness,
        DownmixMode downmix,
        string? customMatrix
    )
    {
        string? result = AudioFilterBuilder.BuildAudioFilter(loudness, downmix, customMatrix);
        result.Should().BeNull();
    }
}
