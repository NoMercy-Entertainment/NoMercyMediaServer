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
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.AdvancedFeatures;

public class SubtitleFeaturesTests
{
    [Theory]
    [InlineData(data: ["srt", true])]
    [InlineData(data: ["SRT", true])]
    [InlineData(data: ["subrip", true])]
    [InlineData(data: ["ass", true])]
    [InlineData(data: ["ssa", true])]
    [InlineData(data: ["webvtt", true])]
    [InlineData(data: ["mov_text", true])]
    [InlineData(data: ["text", true])]
    public void SubtitleClassifier_TextBased_ReturnsTrue(string codec, bool expected)
    {
        bool result = SubtitleClassifier.IsTextBased(codec: codec);
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["hdmv_pgs_subtitle", true])]
    [InlineData(data: ["HDMV_PGS_SUBTITLE", true])]
    [InlineData(data: ["dvd_subtitle", true])]
    [InlineData(data: ["dvb_subtitle", true])]
    public void SubtitleClassifier_BitmapBased_ReturnsTrue(string codec, bool expected)
    {
        bool result = SubtitleClassifier.IsBitmapBased(codec: codec);
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "hdmv_pgs_subtitle")]
    [InlineData(data: "dvd_subtitle")]
    [InlineData(data: "dvb_subtitle")]
    public void SubtitleClassifier_BitmapCodecs_AreNotTextBased(string codec)
    {
        SubtitleClassifier.IsTextBased(codec: codec).Should().BeFalse();
    }

    [Theory]
    [InlineData(data: "srt")]
    [InlineData(data: "ass")]
    [InlineData(data: "webvtt")]
    public void SubtitleClassifier_TextCodecs_AreNotBitmapBased(string codec)
    {
        SubtitleClassifier.IsBitmapBased(codec: codec).Should().BeFalse();
    }

    [Fact]
    public void SubtitleClassifier_UnknownCodec_ReturnsFalseForBoth()
    {
        SubtitleClassifier.IsTextBased(codec: "unknown_codec").Should().BeFalse();
        SubtitleClassifier.IsBitmapBased(codec: "unknown_codec").Should().BeFalse();
    }

    [Fact]
    public void WhisperModelSize_HasExpectedValues()
    {
        WhisperModelSize[] values = Enum.GetValues<WhisperModelSize>();

        values.Should().Contain(expected: WhisperModelSize.Tiny);
        values.Should().Contain(expected: WhisperModelSize.Base);
        values.Should().Contain(expected: WhisperModelSize.Small);
        values.Should().Contain(expected: WhisperModelSize.Medium);
        values.Should().Contain(expected: WhisperModelSize.LargeV2);
        values.Should().Contain(expected: WhisperModelSize.LargeV3);
        values.Should().HaveCount(expected: 6);
    }

    [Fact]
    public void WhisperOptions_ConstructsWithDefaults()
    {
        WhisperOptions options = new(ModelPath: "/models/whisper-large-v3.bin");

        options.ModelPath.Should().Be(expected: "/models/whisper-large-v3.bin");
        options.ModelSize.Should().Be(expected: WhisperModelSize.LargeV3);
        options.TranslateToEnglish.Should().BeFalse();
        options.MaxSegmentLengthMs.Should().Be(expected: 10000);
    }

    [Fact]
    public void WhisperOptions_ConstructsWithCustomValues()
    {
        WhisperOptions options = new(
            ModelPath: "/models/whisper-tiny.bin",
            ModelSize: WhisperModelSize.Tiny,
            TranslateToEnglish: true,
            MaxSegmentLengthMs: 5000
        );

        options.ModelPath.Should().Be(expected: "/models/whisper-tiny.bin");
        options.ModelSize.Should().Be(expected: WhisperModelSize.Tiny);
        options.TranslateToEnglish.Should().BeTrue();
        options.MaxSegmentLengthMs.Should().Be(expected: 5000);
    }

    [Fact]
    public void SubtitleTrack_ConstructsCorrectly()
    {
        SubtitleTrack track = new(
            FilePath: "/output/subtitles/en.vtt",
            Language: "en",
            Format: SubtitleCodecType.WebVtt,
            CueCount: 482
        );

        track.FilePath.Should().Be(expected: "/output/subtitles/en.vtt");
        track.Language.Should().Be(expected: "en");
        track.Format.Should().Be(expected: SubtitleCodecType.WebVtt);
        track.CueCount.Should().Be(expected: 482);
    }

    [Fact]
    public void SubtitleTrack_SrtFormat_ConstructsCorrectly()
    {
        SubtitleTrack track = new(
            FilePath: "/output/subs/fr.srt",
            Language: "fr",
            Format: SubtitleCodecType.Srt,
            CueCount: 120
        );

        track.Format.Should().Be(expected: SubtitleCodecType.Srt);
        track.Language.Should().Be(expected: "fr");
    }

    [Fact]
    public void SubtitleTrack_AssFormat_ConstructsCorrectly()
    {
        SubtitleTrack track = new(
            FilePath: "/output/subs/ja.ass",
            Language: "ja",
            Format: SubtitleCodecType.Ass,
            CueCount: 300
        );

        track.Format.Should().Be(expected: SubtitleCodecType.Ass);
    }
}
