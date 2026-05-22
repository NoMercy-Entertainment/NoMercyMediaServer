using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// SubtitleClassifier routes subtitle streams to the correct extraction
/// path: text codecs get a plain ffmpeg extract (fast, lossless); bitmap
/// codecs need OCR (slow, error-prone). Misclassifying a bitmap codec as
/// "text" fails the encode with an unhelpful "non-text subtitle to
/// WebVTT" error; misclassifying a text codec as "bitmap" kicks off
/// pointless OCR.
/// </summary>
public class SubtitleClassifierTests
{
    [Theory]
    [InlineData("srt")]
    [InlineData("subrip")]
    [InlineData("ass")]
    [InlineData("ssa")]
    [InlineData("webvtt")]
    [InlineData("mov_text")]
    [InlineData("text")]
    public void IsTextBased_KnownTextCodecs_ReturnsTrue(string codec)
    {
        SubtitleClassifier.IsTextBased(codec).Should().BeTrue();
    }

    [Theory]
    [InlineData("hdmv_pgs_subtitle")]
    [InlineData("dvd_subtitle")]
    [InlineData("dvb_subtitle")]
    public void IsBitmapBased_KnownBitmapCodecs_ReturnsTrue(string codec)
    {
        SubtitleClassifier.IsBitmapBased(codec).Should().BeTrue();
    }

    [Fact]
    public void IsTextBased_CaseInsensitive()
    {
        // ffprobe output is typically lowercase but user-facing / mkv
        // tooling can emit uppercase variants. Classifier must accept both.
        SubtitleClassifier.IsTextBased("SRT").Should().BeTrue();
        SubtitleClassifier.IsTextBased("SubRip").Should().BeTrue();
        SubtitleClassifier.IsTextBased("ASS").Should().BeTrue();
    }

    [Fact]
    public void IsBitmapBased_CaseInsensitive()
    {
        SubtitleClassifier.IsBitmapBased("HDMV_PGS_SUBTITLE").Should().BeTrue();
        SubtitleClassifier.IsBitmapBased("DVD_Subtitle").Should().BeTrue();
    }

    [Fact]
    public void PgsNotClassifiedAsText()
    {
        // PGS is the most common trap — bluray subs. Misclassifying as
        // text would pipe the bitmap stream into "extract to webvtt"
        // which outputs nothing useful.
        SubtitleClassifier.IsTextBased("hdmv_pgs_subtitle").Should().BeFalse();
    }

    [Fact]
    public void SrtNotClassifiedAsBitmap()
    {
        SubtitleClassifier.IsBitmapBased("srt").Should().BeFalse();
    }

    [Fact]
    public void UnknownCodec_ClassifiedAsNeither()
    {
        // e.g. "cc_data" (708/608 captions) — we don't handle these yet.
        // The classifier must say no to both paths so the extractor
        // logs "unsupported" rather than corrupting data.
        SubtitleClassifier.IsTextBased("cc_data").Should().BeFalse();
        SubtitleClassifier.IsBitmapBased("cc_data").Should().BeFalse();
    }

    [Fact]
    public void EmptyCodec_ClassifiedAsNeither()
    {
        SubtitleClassifier.IsTextBased("").Should().BeFalse();
        SubtitleClassifier.IsBitmapBased("").Should().BeFalse();
    }

    [Fact]
    public void TextAndBitmapCategories_AreDisjoint()
    {
        // No codec should appear in both sets — that would mean the
        // extractor has no deterministic path. Check every known text
        // codec is NOT bitmap, and every known bitmap codec is NOT text.
        string[] textCodecs = ["srt", "subrip", "ass", "ssa", "webvtt", "mov_text", "text"];
        string[] bitmapCodecs = ["hdmv_pgs_subtitle", "dvd_subtitle", "dvb_subtitle"];

        foreach (string t in textCodecs)
            SubtitleClassifier.IsBitmapBased(t).Should().BeFalse();
        foreach (string b in bitmapCodecs)
            SubtitleClassifier.IsTextBased(b).Should().BeFalse();
    }

    // ── ResolveVariant ──────────────────────────────────────────────────────

    private static SubtitleStreamInfo Stream(
        string? title = null,
        bool isForced = false,
        bool isDefault = false
    ) =>
        new(
            Index: 0,
            Codec: "srt",
            Language: "eng",
            IsDefault: isDefault,
            IsForced: isForced,
            Title: title
        );

    [Theory]
    [InlineData("Signs & Songs")]
    [InlineData("s&s")]
    [InlineData("English [Signs]")]
    [InlineData("English (Songs)")]
    public void ResolveVariant_TitleMentionsSignsOrSongs_ReturnsSign(string title)
    {
        SubtitleClassifier.ResolveVariant(Stream(title: title)).Should().Be("sign");
    }

    [Theory]
    [InlineData("SDH")]
    [InlineData("English SDH")]
    [InlineData("English (Hearing Impaired)")]
    public void ResolveVariant_TitleMentionsSdhOrHearing_ReturnsSdh(string title)
    {
        SubtitleClassifier.ResolveVariant(Stream(title: title)).Should().Be("sdh");
    }

    [Fact]
    public void ResolveVariant_ForcedFlag_WithoutTitle_ReturnsSign()
    {
        // Forced subs are typically signs / foreign-language passages.
        SubtitleClassifier.ResolveVariant(Stream(isForced: true)).Should().Be("sign");
    }

    [Fact]
    public void ResolveVariant_DefaultFlag_WithoutTitleOrForced_ReturnsFull()
    {
        SubtitleClassifier.ResolveVariant(Stream(isDefault: true)).Should().Be("full");
    }

    [Fact]
    public void ResolveVariant_NoFlags_ReturnsAlt()
    {
        // Secondary tracks land in alt — the player UI groups them under
        // a non-default listing.
        SubtitleClassifier.ResolveVariant(Stream()).Should().Be("alt");
    }

    [Fact]
    public void ResolveVariant_TitleTakesPrecedenceOverForcedFlag()
    {
        // Title beats disposition flags so a "Sign & Song" track flagged
        // as default doesn't land in "full".
        SubtitleClassifier
            .ResolveVariant(Stream(title: "SDH", isForced: true, isDefault: true))
            .Should()
            .Be("sdh");
    }

    [Fact]
    public void ResolveVariant_TitleCaseInsensitive()
    {
        SubtitleClassifier.ResolveVariant(Stream(title: "SDH")).Should().Be("sdh");
        SubtitleClassifier.ResolveVariant(Stream(title: "sdh")).Should().Be("sdh");
        SubtitleClassifier.ResolveVariant(Stream(title: "Sign")).Should().Be("sign");
        SubtitleClassifier.ResolveVariant(Stream(title: "SIGNS")).Should().Be("sign");
    }
}
