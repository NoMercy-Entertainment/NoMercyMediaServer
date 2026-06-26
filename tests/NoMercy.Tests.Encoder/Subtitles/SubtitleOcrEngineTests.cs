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

using System.Reflection;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

public class SubtitleOcrEngineParserTests
{
    [Fact]
    public void Parse_SingleTextBlock_EmitsOneCue()
    {
        string input = """
            frame:0    pts:0    pts_time:0
            lavfi.ocr.text=Hello world

            frame:24   pts:24000 pts_time:1.0
            lavfi.ocr.text=Hello world
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(input);

        Assert.Single(cues);
        Assert.Equal(0, cues[0].StartSeconds);
        Assert.Equal(1.0, cues[0].EndSeconds);
        Assert.Equal("Hello world", cues[0].Text);
    }

    [Fact]
    public void Parse_TextChange_EmitsTwoCues()
    {
        string input = """
            pts_time:0
            lavfi.ocr.text=Hello

            pts_time:1
            lavfi.ocr.text=Hello

            pts_time:2
            lavfi.ocr.text=World
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(input);

        Assert.Equal(2, cues.Count);
        Assert.Equal("Hello", cues[0].Text);
        Assert.Equal(0, cues[0].StartSeconds);
        Assert.Equal(1.0, cues[0].EndSeconds);
        Assert.Equal("World", cues[1].Text);
        Assert.Equal(2.0, cues[1].StartSeconds);
    }

    [Fact]
    public void Parse_EmptyTextTerminatesCurrentCue()
    {
        string input = """
            pts_time:0
            lavfi.ocr.text=Hello

            pts_time:1
            lavfi.ocr.text=Hello

            pts_time:2
            lavfi.ocr.text=
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(input);

        Assert.Single(cues);
        Assert.Equal("Hello", cues[0].Text);
        Assert.Equal(1.0, cues[0].EndSeconds);
    }

    [Fact]
    public void Parse_NoOcrLines_ReturnsEmpty()
    {
        string input = """
            frame:0 pts:0 pts_time:0
            frame:1 pts:1 pts_time:1
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(input);

        Assert.Empty(cues);
    }

    [Fact]
    public void Parse_CrlfLineEndings_WorksTheSame()
    {
        string input =
            "pts_time:0\r\nlavfi.ocr.text=Hello\r\n\r\npts_time:1\r\nlavfi.ocr.text=Hello\r\n";

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(input);

        Assert.Single(cues);
        Assert.Equal("Hello", cues[0].Text);
    }

    [Fact]
    public void Parse_FinalCueWithoutExplicitEnd_StillEmitsCue()
    {
        string input = """
            pts_time:10.5
            lavfi.ocr.text=Last line
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(input);

        Assert.Single(cues);
        Assert.Equal("Last line", cues[0].Text);
        Assert.Equal(10.5, cues[0].StartSeconds);
        Assert.Equal(10.5, cues[0].EndSeconds);
    }

    [Fact]
    public void Parse_FractionalPtsTime_PreservesPrecision()
    {
        string input = """
            pts_time:0.041667
            lavfi.ocr.text=Frame 1

            pts_time:2.083333
            lavfi.ocr.text=Frame 1
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(input);

        Assert.Single(cues);
        Assert.Equal(0.041667, cues[0].StartSeconds, precision: 6);
        Assert.Equal(2.083333, cues[0].EndSeconds, precision: 6);
    }

    /// <summary>
    /// Trampoline around the internal parser method so tests can call it without
    /// using InternalsVisibleTo.
    /// </summary>
    private static class ParserAccess
    {
        public static List<SubtitleOcrEngine.SubtitleCue> Parse(string content) =>
            (List<SubtitleOcrEngine.SubtitleCue>)
                typeof(SubtitleOcrEngine)
                    .GetMethod("ParseOcrOutput", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [content])!;
    }
}

public class SubtitleOcrEngineEscapeFilterPathTests
{
    [Fact]
    public void EscapeFilterPath_WindowsAbsolutePath_DoubleEscapesColon()
    {
        // C:\Users\patri\AppData\Local\Temp\nm-ocr\foo.txt
        // Expected emitted string: C\\:/Users/patri/AppData/Local/Temp/nm-ocr/foo.txt
        // FFmpeg filterchain layer strips one backslash → C\:/...
        // Filter-option layer strips the remaining backslash → C:/...  (literal colon preserved)
        string input = @"C:\Users\patri\AppData\Local\Temp\nm-ocr\foo.txt";
        string result = EscapeAccess.EscapeFilterPath(input);

        result.Should().Be(@"C\\:/Users/patri/AppData/Local/Temp/nm-ocr/foo.txt");
    }

    [Fact]
    public void EscapeFilterPath_UnixPath_NoColonNoChange()
    {
        string input = "/tmp/nm-ocr/foo.txt";
        string result = EscapeAccess.EscapeFilterPath(input);

        result.Should().Be("/tmp/nm-ocr/foo.txt");
    }

    [Fact]
    public void EscapeFilterPath_ForwardSlashWindowsPath_DoubleEscapesColon()
    {
        // Already-normalised path still has a drive colon
        string input = "C:/Users/patri/AppData/Local/Temp/nm-ocr/foo.txt";
        string result = EscapeAccess.EscapeFilterPath(input);

        result.Should().Be(@"C\\:/Users/patri/AppData/Local/Temp/nm-ocr/foo.txt");
    }

    private static class EscapeAccess
    {
        public static string EscapeFilterPath(string path) =>
            (string)
                typeof(SubtitleOcrEngine)
                    .GetMethod("EscapeFilterPath", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [path])!;
    }
}
