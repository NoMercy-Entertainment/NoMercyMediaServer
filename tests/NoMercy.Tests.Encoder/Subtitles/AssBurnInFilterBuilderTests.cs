using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// AssBurnInFilterBuilder produces the correct <c>ass=</c> filter
/// expression with FFmpeg filter-graph escaping applied.
/// </summary>
public class AssBurnInFilterBuilderTests
{
    private readonly AssBurnInFilterBuilder _builder = new();

    [Fact]
    public void Build_SimplePath_ReturnsAssFilter()
    {
        string result = _builder.Build("/path/to/subtitle.ass");

        Assert.Equal("ass='/path/to/subtitle.ass'", result);
    }

    [Fact]
    public void Build_PathWithColon_EscapesColon()
    {
        // Windows-style path with drive letter colon
        string result = _builder.Build("C:/movies/subtitle.ass");

        Assert.Contains("C\\:/movies/subtitle.ass", result);
    }

    [Fact]
    public void Build_PathWithBackslashes_NormalisesToForwardSlashThenEscapesColon()
    {
        // Windows path with backslash separators
        string result = _builder.Build("C:\\movies\\my film\\subtitle.ass");

        // Backslashes normalised to forward slashes; colon on drive letter escaped
        Assert.Contains("C\\:/movies/my film/subtitle.ass", result);
    }

    [Fact]
    public void Build_WithFontDirectory_AppendsFontsDirOption()
    {
        string result = _builder.Build("/path/to/subtitle.ass", "/output/fonts");

        Assert.StartsWith("ass='", result);
        Assert.Contains(":fontsdir=/output/fonts", result);
    }

    [Fact]
    public void Build_WithNullFontDirectory_OmitsFontsDirOption()
    {
        string result = _builder.Build("/path/to/subtitle.ass", fontDirectory: null);

        Assert.DoesNotContain("fontsdir", result);
    }

    [Fact]
    public void Build_WithEmptyFontDirectory_OmitsFontsDirOption()
    {
        string result = _builder.Build("/path/to/subtitle.ass", fontDirectory: "");

        Assert.DoesNotContain("fontsdir", result);
    }

    [Fact]
    public void Build_PathWithColonInFilename_EscapesAllColons()
    {
        // Edge case: filename that contains a colon (non-Windows, unusual but valid)
        string result = _builder.Build("/media/show:s01e01.ass");

        Assert.Contains("/media/show\\:s01e01.ass", result);
    }

    [Fact]
    public void EscapeForFilterGraph_BackslashPath_NormalisesAndEscapes()
    {
        string result = AssBurnInFilterBuilder.EscapeForFilterGraph("C:\\path\\file.ass");

        // Backslashes → forward slashes, colon escaped
        Assert.Equal("C\\:/path/file.ass", result);
    }

    [Fact]
    public void EscapeForFilterGraph_UnixPath_OnlyEscapesColons()
    {
        string result = AssBurnInFilterBuilder.EscapeForFilterGraph("/usr/media/sub.ass");

        // No colons → no escaping needed; no backslashes → no change
        Assert.Equal("/usr/media/sub.ass", result);
    }
}
