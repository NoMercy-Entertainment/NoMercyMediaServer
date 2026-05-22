using NoMercy.Encoder.Core;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class SubtitleParserTests
{
    [Theory]
    [InlineData("& Singing in the rain", "♪ Singing in the rain")]
    [InlineData("J Hello world", "♪ Hello world")]
    [InlineData("I Music plays", "♪ Music plays")]
    [InlineData("' La la la", "♪ La la la")]
    public void PostProcessOcrText_LatinUppercase_ReplacesMisreadMusicNote(
        string input,
        string expected
    )
    {
        Assert.Equal(expected, SubtitleParser.PostProcessOcrText(input));
    }

    [Theory]
    [InlineData("& 唐山大兄", "♪ 唐山大兄")] // CJK Han
    [InlineData("J 한국어", "♪ 한국어")] // Hangul
    [InlineData("I Привет", "♪ Привет")] // Cyrillic
    [InlineData("' مرحبا", "♪ مرحبا")] // Arabic
    [InlineData("& Γεια σου", "♪ Γεια σου")] // Greek
    [InlineData("J こんにちは", "♪ こんにちは")] // Hiragana
    public void PostProcessOcrText_NonLatinScript_ReplacesMisreadMusicNote(
        string input,
        string expected
    )
    {
        // Tesseract still emits ASCII for the unrecognized ♪ glyph even when the
        // surrounding text is in a non-Latin script. The original [A-Z] lookahead
        // skipped these subs entirely; \p{L} accepts any Unicode letter.
        Assert.Equal(expected, SubtitleParser.PostProcessOcrText(input));
    }

    [Theory]
    [InlineData("- & Hello", "- ♪ Hello")]
    [InlineData("-& Hello", "-♪ Hello")]
    [InlineData("- J 漢字", "- ♪ 漢字")]
    public void PostProcessOcrText_DialogPrefix_PreservesPrefix(string input, string expected)
    {
        Assert.Equal(expected, SubtitleParser.PostProcessOcrText(input));
    }

    [Theory]
    [InlineData("&' Hello", "♪ Hello")]
    [InlineData("JJ World", "♪ World")]
    [InlineData("II 漢字", "♪ 漢字")]
    [InlineData("&J こんにちは", "♪ こんにちは")]
    public void PostProcessOcrText_DoubleMisread_ReplacesWithSingleNote(
        string input,
        string expected
    )
    {
        Assert.Equal(expected, SubtitleParser.PostProcessOcrText(input));
    }

    [Theory]
    [InlineData("Hello & World")] // ampersand mid-sentence — not a music note
    [InlineData("Just text")]
    [InlineData("123 numbers")]
    [InlineData("")]
    public void PostProcessOcrText_NoMatch_LeavesUnchanged(string input)
    {
        Assert.Equal(input, SubtitleParser.PostProcessOcrText(input));
    }

    [Fact]
    public void PostProcessOcrText_MultiLine_FixesEachLineIndependently()
    {
        string input = "& First line\n& Second line\n& 第三行";
        string expected = "♪ First line\n♪ Second line\n♪ 第三行";
        Assert.Equal(expected, SubtitleParser.PostProcessOcrText(input));
    }
}
