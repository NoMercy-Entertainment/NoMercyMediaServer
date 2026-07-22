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
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "subrip", language: "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Extension.Should().Be(expected: "vtt");
        info.FfmpegCodec.Should().Be(expected: "webvtt");
        info.IsBitmap.Should().BeFalse();
        info.OutputPath.Should().Contain(expected: "subtitles");
        info.OutputPath.Should().EndWith(expected: ".vtt");
    }

    // ------------------------------------------------------------------
    // ASS subtitle stays as ASS
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_Ass_StaysAsAss()
    {
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "ass", language: "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Extension.Should().Be(expected: "ass");
        info.FfmpegCodec.Should().Be(expected: "ass");
        info.IsBitmap.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // SSA subtitle also stays as ASS
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_Ssa_StaysAsAss()
    {
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "ssa", language: "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Extension.Should().Be(expected: "ass");
        info.FfmpegCodec.Should().Be(expected: "ass");
    }

    // ------------------------------------------------------------------
    // VobSub (dvd_subtitle) → native .idx/.sub pair via vobsubenc muxer.
    // PGS / DVB stay in .mks (see PgsSubtitle test below).
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_DvdSubtitle_ProducesIdxFile()
    {
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "dvd_subtitle", language: "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Extension.Should().Be(expected: "idx");
        info.FfmpegCodec.Should().Be(expected: "copy");
        info.IsBitmap.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // PGS subtitle (bitmap) → .mks with copy codec
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_PgsSubtitle_ProducesMksFile()
    {
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "hdmv_pgs_subtitle", language: "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Extension.Should().Be(expected: "mks");
        info.FfmpegCodec.Should().Be(expected: "copy");
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
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng") with { Variant = "sign" };
        SubtitleStreamInfo stream = new(
            Index: 0,
            Codec: "subrip",
            Language: "eng",
            IsDefault: false,
            IsForced: true
        );

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Variant.Should().Be(expected: "sign");
    }

    [Fact]
    public void ResolveOutput_PropagatesPlanVariant_ForAlt()
    {
        // Second English track in the source — PlanStage promotes the first
        // to "full" and demotes peers to "alt". The extractor must honour
        // that decision, not re-classify and collide both back to "full".
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 1, language: "eng") with
        {
            Variant = "alt",
        };
        SubtitleStreamInfo stream = MakeStream(index: 1, codec: "subrip", language: "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Variant.Should().Be(expected: "alt");
    }

    // ------------------------------------------------------------------
    // Output path uses template with subtitles/ directory
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_OutputPath_UsesSubtitlesDirectory()
    {
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "subrip", language: "eng");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.OutputPath.Should().StartWith(expected: "subtitles/");
    }

    // ------------------------------------------------------------------
    // Language comes from stream, not plan
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_LanguageFromStream_NotPlan()
    {
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "und");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "subrip", language: "fra");

        SubtitleOutputInfo info = _extractor.ResolveOutput(plan: plan, stream: stream, outputDirectory: OutputDir, mediaTitle: MediaTitle);

        info.Language.Should().Be(expected: "fra");
    }

    // ------------------------------------------------------------------
    // Playlist URI resolution
    // ------------------------------------------------------------------

    [Fact]
    public void ResolvePlaylistUri_TextSubtitle_VttExtension()
    {
        SubtitleOutputPlan plan = MakePlan(sourceIndex: 0, language: "eng");
        SubtitleStreamInfo stream = MakeStream(index: 0, codec: "subrip", language: "eng");

        string uri = _extractor.ResolvePlaylistUri(plan: plan, stream: stream, mediaTitle: MediaTitle);

        uri.Should().EndWith(expected: ".vtt");
        uri.Should().Contain(expected: "subtitles/");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static SubtitleOutputPlan MakePlan(int sourceIndex, string language)
    {
        return new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Extract,
            Language: language,
            SourceIndex: sourceIndex,
            MapLabel: $"0:s:{sourceIndex}"
        );
    }

    private static SubtitleStreamInfo MakeStream(int index, string codec, string language)
    {
        return new(
            Index: index,
            Codec: codec,
            Language: language,
            IsDefault: index == 0,
            IsForced: false
        );
    }
}
