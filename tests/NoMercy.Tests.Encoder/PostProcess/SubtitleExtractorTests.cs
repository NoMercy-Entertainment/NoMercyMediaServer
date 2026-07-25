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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.PostProcess;

namespace NoMercy.Tests.Encoder.PostProcess;

public class SubtitleExtractorTests
{
    private const string OutputDir = "/output";
    private const string MediaTitle = "Movie.Name.NoMercy";

    private readonly SubtitleExtractor _extractor = new();

    // ------------------------------------------------------------------
    // Text subtitle (subrip) → WebVTT output
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_Subrip_ProducesWebVtt()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "subrip", "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Extension.Should().Be("vtt");
        info.FfmpegCodec.Should().Be("webvtt");
        info.IsBitmap.Should().BeFalse();
        info.OutputPath.Should().Contain("subtitles");
        info.OutputPath.Should().EndWith(".vtt");
    }

    // ------------------------------------------------------------------
    // ASS subtitle stays as ASS
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_Ass_StaysAsAss()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "ass", "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Extension.Should().Be("ass");
        info.FfmpegCodec.Should().Be("ass");
        info.IsBitmap.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // SSA subtitle also stays as ASS
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_Ssa_StaysAsAss()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "ssa", "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Extension.Should().Be("ass");
        info.FfmpegCodec.Should().Be("ass");
    }

    // ------------------------------------------------------------------
    // VobSub (dvd_subtitle) → native .idx/.sub pair via vobsubenc muxer.
    // PGS / DVB stay in .mks (see PgsSubtitle test below).
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_DvdSubtitle_ProducesIdxFile()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "dvd_subtitle", "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Extension.Should().Be("idx");
        info.FfmpegCodec.Should().Be("copy");
        info.IsBitmap.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // PGS subtitle (bitmap) → .mks with copy codec
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_PgsSubtitle_ProducesMksFile()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "hdmv_pgs_subtitle", "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Extension.Should().Be("mks");
        info.FfmpegCodec.Should().Be("copy");
        info.IsBitmap.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Variant propagation — the extractor trusts the plan's pre-classified
    // variant rather than re-running classification per stream. PlanStage
    // sees every source subtitle at once, so it can disambiguate
    // full / alt across same-language peers; the extractor cannot.
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_PropagatesPlanVariant_ForSign()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng") with { Variant = "sign" };
        SubtitleStreamInfo stream = new(
            0,
            "subrip",
            "eng",
            false,
            true
        );

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Variant.Should().Be("sign");
    }

    [Fact]
    public void ResolveOutput_PropagatesPlanVariant_ForAlt()
    {
        // Second English track in the source — PlanStage promotes the first
        // to "full" and demotes peers to "alt". The extractor must honour
        // that decision, not re-classify and collide both back to "full".
        SubtitleOutputPlan plan = MakePlan(1, "eng") with
        {
            Variant = "alt",
        };
        SubtitleStreamInfo stream = MakeStream(1, "subrip", "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Variant.Should().Be("alt");
    }

    // ------------------------------------------------------------------
    // Output path uses template with subtitles/ directory
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_OutputPath_UsesSubtitlesDirectory()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "subrip", "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.OutputPath.Should().StartWith("subtitles/");
    }

    // ------------------------------------------------------------------
    // Language comes from stream, not plan
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_LanguageFromStream_NotPlan()
    {
        SubtitleOutputPlan plan = MakePlan(0, "und");
        SubtitleStreamInfo stream = MakeStream(0, "subrip", "fra");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan, stream, OutputDir, MediaTitle);

        info.Language.Should().Be("fra");
    }

    // ------------------------------------------------------------------
    // Playlist URI resolution
    // ------------------------------------------------------------------

    [Fact]
    public void ResolvePlaylistUri_TextSubtitle_VttExtension()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "subrip", "eng");

        string uri = _extractor.ResolvePlaylistUri(plan, stream, MediaTitle);

        uri.Should().EndWith(".vtt");
        uri.Should().Contain("subtitles/");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static SubtitleOutputPlan MakePlan(int sourceIndex, string language)
    {
        return new(
            SubtitleCodecType.WebVtt,
            StreamAction.Extract,
            language,
            sourceIndex,
            $"0:s:{sourceIndex}"
        );
    }

    private static SubtitleStreamInfo MakeStream(int index, string codec, string language)
    {
        return new(
            index,
            codec,
            language,
            index == 0,
            false
        );
    }
}
