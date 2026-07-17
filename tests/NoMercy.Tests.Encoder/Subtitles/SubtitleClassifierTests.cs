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
    [InlineData("pgs")] // NoMercy short alias for hdmv_pgs_subtitle
    [InlineData("dvd_subtitle")]
    [InlineData("vobsub")] // libavformat alternative name for dvd_subtitle
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
        string[] bitmapCodecs =
        [
            "hdmv_pgs_subtitle",
            "pgs",
            "dvd_subtitle",
            "vobsub",
            "dvb_subtitle",
        ];

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
    public void ResolveVariant_NoFlags_ReturnsFull()
    {
        // Single-stream context: a track that isn't sign / sdh / forced is
        // the regular language track. "alt" is reserved for the per-language
        // overflow case (see ResolveVariants) — it shouldn't be the fallback
        // for a stream with no peer context.
        SubtitleClassifier.ResolveVariant(Stream()).Should().Be("full");
    }

    // ── ResolveVariants (multi-stream, per-language disambiguation) ─────────

    [Fact]
    public void ResolveVariants_SingleStreamPerLanguage_AllFull()
    {
        IReadOnlyList<SubtitleStreamInfo> streams =
        [
            new(
                Index: 0,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 1,
                Codec: "srt",
                Language: "nld",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
        ];

        IReadOnlyList<string> variants = SubtitleClassifier.ResolveVariants(streams);

        variants.Should().Equal("full", "full");
    }

    [Fact]
    public void ResolveVariants_MultipleSameLanguage_FirstFullRestAlt()
    {
        IReadOnlyList<SubtitleStreamInfo> streams =
        [
            new(
                Index: 0,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 1,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 2,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
        ];

        IReadOnlyList<string> variants = SubtitleClassifier.ResolveVariants(streams);

        variants.Should().Equal("full", "alt", "alt");
    }

    [Fact]
    public void ResolveVariants_DefaultFlagWinsAsFullEvenWhenNotFirst()
    {
        IReadOnlyList<SubtitleStreamInfo> streams =
        [
            new(
                Index: 0,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 1,
                Codec: "srt",
                Language: "eng",
                IsDefault: true,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 2,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
        ];

        IReadOnlyList<string> variants = SubtitleClassifier.ResolveVariants(streams);

        variants.Should().Equal("alt", "full", "alt");
    }

    [Fact]
    public void ResolveVariants_PreClassifiedSignAndSdh_DontConsumeFullSlot()
    {
        // Sign and SDH tracks shouldn't compete for the per-language "full"
        // slot — they pre-classify by title / forced flag and the regular
        // un-flagged track still becomes the language's "full".
        IReadOnlyList<SubtitleStreamInfo> streams =
        [
            new(
                Index: 0,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: "English [SDH]"
            ),
            new(
                Index: 1,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: true,
                Title: null
            ),
            new(
                Index: 2,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
        ];

        IReadOnlyList<string> variants = SubtitleClassifier.ResolveVariants(streams);

        variants.Should().Equal("sdh", "sign", "full");
    }

    [Fact]
    public void ResolveVariants_MixedLanguagesShareNoAltSlots()
    {
        // Per-language disambiguation must not leak across languages —
        // English alt + Dutch full + Dutch alt is the expected shape.
        IReadOnlyList<SubtitleStreamInfo> streams =
        [
            new(
                Index: 0,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 1,
                Codec: "srt",
                Language: "eng",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 2,
                Codec: "srt",
                Language: "nld",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 3,
                Codec: "srt",
                Language: "nld",
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
        ];

        IReadOnlyList<string> variants = SubtitleClassifier.ResolveVariants(streams);

        variants.Should().Equal("full", "alt", "full", "alt");
    }

    [Fact]
    public void ResolveVariants_NullLanguage_TreatedAsUnd()
    {
        IReadOnlyList<SubtitleStreamInfo> streams =
        [
            new(
                Index: 0,
                Codec: "srt",
                Language: null,
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
            new(
                Index: 1,
                Codec: "srt",
                Language: null,
                IsDefault: false,
                IsForced: false,
                Title: null
            ),
        ];

        IReadOnlyList<string> variants = SubtitleClassifier.ResolveVariants(streams);

        variants.Should().Equal("full", "alt");
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

    // ── Bitmap sidecar extensions ────────────────────────────────────────────
    // The library scan and the playback track list both decide from a filename
    // alone whether a sidecar is a bitmap subtitle. Publishing one as a track
    // gives the player an entry it can list but never render.

    [Theory]
    [InlineData("mks")]
    [InlineData("sup")]
    [InlineData("idx")]
    [InlineData("vob")]
    public void IsBitmapSidecarExtension_IsTrueForEveryFormatTheExtractionPassWrites(string ext)
    {
        SubtitleClassifier.IsBitmapSidecarExtension(ext).Should().BeTrue();
    }

    [Fact]
    public void IsBitmapSidecarExtension_IsTrueForMks_TheFormatTheExtractionPassActuallyWrites()
    {
        // Guarded on its own: a list of just sup/vob let every extracted bitmap
        // track through, so each one was published alongside its OCR sidecar and
        // every subtitle appeared twice in the player.
        SubtitleClassifier.IsBitmapSidecarExtension("mks").Should().BeTrue();
    }

    [Theory]
    [InlineData("vtt")]
    [InlineData("srt")]
    [InlineData("ass")]
    [InlineData("ssa")]
    public void IsBitmapSidecarExtension_IsFalseForTextFormats(string ext)
    {
        SubtitleClassifier.IsBitmapSidecarExtension(ext).Should().BeFalse();
    }

    [Fact]
    public void IsBitmapSidecarExtension_AcceptsALeadingDotAndAnyCasing()
    {
        // Callers reach it from Path.GetExtension (".MKS") and from a regex
        // capture group ("mks") alike.
        SubtitleClassifier.IsBitmapSidecarExtension(".mks").Should().BeTrue();
        SubtitleClassifier.IsBitmapSidecarExtension(".MKS").Should().BeTrue();
        SubtitleClassifier.IsBitmapSidecarExtension("MKS").Should().BeTrue();
        SubtitleClassifier.IsBitmapSidecarExtension(".VTT").Should().BeFalse();
    }
}
