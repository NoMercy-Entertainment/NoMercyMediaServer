namespace NoMercy.Tests.Encoder.V3.AdvancedFeatures;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Subtitles;

public class SubtitleFeaturesTests
{
    [Theory]
    [InlineData("srt", true)]
    [InlineData("SRT", true)]
    [InlineData("subrip", true)]
    [InlineData("ass", true)]
    [InlineData("ssa", true)]
    [InlineData("webvtt", true)]
    [InlineData("mov_text", true)]
    [InlineData("text", true)]
    public void SubtitleClassifier_TextBased_ReturnsTrue(string codec, bool expected)
    {
        bool result = SubtitleClassifier.IsTextBased(codec);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("hdmv_pgs_subtitle", true)]
    [InlineData("HDMV_PGS_SUBTITLE", true)]
    [InlineData("dvd_subtitle", true)]
    [InlineData("dvb_subtitle", true)]
    public void SubtitleClassifier_BitmapBased_ReturnsTrue(string codec, bool expected)
    {
        bool result = SubtitleClassifier.IsBitmapBased(codec);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("hdmv_pgs_subtitle")]
    [InlineData("dvd_subtitle")]
    [InlineData("dvb_subtitle")]
    public void SubtitleClassifier_BitmapCodecs_AreNotTextBased(string codec)
    {
        SubtitleClassifier.IsTextBased(codec).Should().BeFalse();
    }

    [Theory]
    [InlineData("srt")]
    [InlineData("ass")]
    [InlineData("webvtt")]
    public void SubtitleClassifier_TextCodecs_AreNotBitmapBased(string codec)
    {
        SubtitleClassifier.IsBitmapBased(codec).Should().BeFalse();
    }

    [Fact]
    public void SubtitleClassifier_UnknownCodec_ReturnsFalseForBoth()
    {
        SubtitleClassifier.IsTextBased("unknown_codec").Should().BeFalse();
        SubtitleClassifier.IsBitmapBased("unknown_codec").Should().BeFalse();
    }

    [Fact]
    public void WhisperModelSize_HasExpectedValues()
    {
        WhisperModelSize[] values = Enum.GetValues<WhisperModelSize>();

        values.Should().Contain(WhisperModelSize.Tiny);
        values.Should().Contain(WhisperModelSize.Base);
        values.Should().Contain(WhisperModelSize.Small);
        values.Should().Contain(WhisperModelSize.Medium);
        values.Should().Contain(WhisperModelSize.LargeV2);
        values.Should().Contain(WhisperModelSize.LargeV3);
        values.Should().HaveCount(6);
    }

    [Fact]
    public void WhisperOptions_ConstructsWithDefaults()
    {
        WhisperOptions options = new(ModelPath: "/models/whisper-large-v3.bin");

        options.ModelPath.Should().Be("/models/whisper-large-v3.bin");
        options.ModelSize.Should().Be(WhisperModelSize.LargeV3);
        options.TranslateToEnglish.Should().BeFalse();
        options.MaxSegmentLengthMs.Should().Be(10000);
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

        options.ModelPath.Should().Be("/models/whisper-tiny.bin");
        options.ModelSize.Should().Be(WhisperModelSize.Tiny);
        options.TranslateToEnglish.Should().BeTrue();
        options.MaxSegmentLengthMs.Should().Be(5000);
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

        track.FilePath.Should().Be("/output/subtitles/en.vtt");
        track.Language.Should().Be("en");
        track.Format.Should().Be(SubtitleCodecType.WebVtt);
        track.CueCount.Should().Be(482);
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

        track.Format.Should().Be(SubtitleCodecType.Srt);
        track.Language.Should().Be("fr");
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

        track.Format.Should().Be(SubtitleCodecType.Ass);
    }
}
