namespace NoMercy.Tests.Encoder.PostProcess;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.PostProcess;

public class SubtitleExtractorTests
{
    private const string OutputDir = "/output";
    private const string MediaTitle = "Movie.Name.NoMercy";

    // ------------------------------------------------------------------
    // Text subtitle (subrip) → WebVTT output
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_Subrip_ProducesWebVtt()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "subrip", "eng");

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

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

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

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

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

        info.Extension.Should().Be("ass");
        info.FfmpegCodec.Should().Be("ass");
    }

    // ------------------------------------------------------------------
    // DVD subtitle (bitmap) → .sub with copy codec
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_DvdSubtitle_ProducesSubFile()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "dvd_subtitle", "eng");

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

        info.Extension.Should().Be("sub");
        info.FfmpegCodec.Should().Be("copy");
        info.IsBitmap.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // PGS subtitle (bitmap) → .sup with copy codec
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_PgsSubtitle_ProducesSupFile()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "hdmv_pgs_subtitle", "eng");

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

        info.Extension.Should().Be("sup");
        info.FfmpegCodec.Should().Be("copy");
        info.IsBitmap.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Forced subtitle variant
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_ForcedSubtitle_VariantIsForced()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = new(
            Index: 0,
            Codec: "subrip",
            Language: "eng",
            IsDefault: false,
            IsForced: true
        );

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

        info.Variant.Should().Be("forced");
    }

    // ------------------------------------------------------------------
    // Output path uses template with subtitles/ directory
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_OutputPath_UsesSubtitlesDirectory()
    {
        SubtitleOutputPlan plan = MakePlan(0, "eng");
        SubtitleStreamInfo stream = MakeStream(0, "subrip", "eng");

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

        info.OutputPath.Should().Contain(Path.Combine(OutputDir, "subtitles"));
    }

    // ------------------------------------------------------------------
    // Language comes from stream, not plan
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveOutput_LanguageFromStream_NotPlan()
    {
        SubtitleOutputPlan plan = MakePlan(0, "und");
        SubtitleStreamInfo stream = MakeStream(0, "subrip", "fra");

        SubtitleOutputInfo info = SubtitleExtractor.ResolveOutput(
            plan,
            stream,
            OutputDir,
            MediaTitle
        );

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

        string uri = SubtitleExtractor.ResolvePlaylistUri(plan, stream, MediaTitle);

        uri.Should().EndWith(".vtt");
        uri.Should().Contain("subtitles/");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static SubtitleOutputPlan MakePlan(int sourceIndex, string language)
    {
        return new SubtitleOutputPlan(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Extract,
            Language: language,
            SourceIndex: sourceIndex,
            MapLabel: $"0:s:{sourceIndex}"
        );
    }

    private static SubtitleStreamInfo MakeStream(int index, string codec, string language)
    {
        return new SubtitleStreamInfo(
            Index: index,
            Codec: codec,
            Language: language,
            IsDefault: index == 0,
            IsForced: false
        );
    }
}
