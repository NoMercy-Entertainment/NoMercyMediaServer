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

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(content: input);

        Assert.Single(collection: cues);
        Assert.Equal(expected: 0, actual: cues[index: 0].StartSeconds);
        Assert.Equal(expected: 1.0, actual: cues[index: 0].EndSeconds);
        Assert.Equal(expected: "Hello world", actual: cues[index: 0].Text);
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

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(content: input);

        Assert.Equal(expected: 2, actual: cues.Count);
        Assert.Equal(expected: "Hello", actual: cues[index: 0].Text);
        Assert.Equal(expected: 0, actual: cues[index: 0].StartSeconds);
        Assert.Equal(expected: 1.0, actual: cues[index: 0].EndSeconds);
        Assert.Equal(expected: "World", actual: cues[index: 1].Text);
        Assert.Equal(expected: 2.0, actual: cues[index: 1].StartSeconds);
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

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(content: input);

        Assert.Single(collection: cues);
        Assert.Equal(expected: "Hello", actual: cues[index: 0].Text);
        Assert.Equal(expected: 1.0, actual: cues[index: 0].EndSeconds);
    }

    [Fact]
    public void Parse_NoOcrLines_ReturnsEmpty()
    {
        string input = """
            frame:0 pts:0 pts_time:0
            frame:1 pts:1 pts_time:1
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(content: input);

        Assert.Empty(collection: cues);
    }

    [Fact]
    public void Parse_CrlfLineEndings_WorksTheSame()
    {
        string input =
            "pts_time:0\r\nlavfi.ocr.text=Hello\r\n\r\npts_time:1\r\nlavfi.ocr.text=Hello\r\n";

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(content: input);

        Assert.Single(collection: cues);
        Assert.Equal(expected: "Hello", actual: cues[index: 0].Text);
    }

    [Fact]
    public void Parse_FinalCueWithoutExplicitEnd_StillEmitsCue()
    {
        string input = """
            pts_time:10.5
            lavfi.ocr.text=Last line
            """;

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(content: input);

        Assert.Single(collection: cues);
        Assert.Equal(expected: "Last line", actual: cues[index: 0].Text);
        Assert.Equal(expected: 10.5, actual: cues[index: 0].StartSeconds);
        Assert.Equal(expected: 10.5, actual: cues[index: 0].EndSeconds);
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

        List<SubtitleOcrEngine.SubtitleCue> cues = ParserAccess.Parse(content: input);

        Assert.Single(collection: cues);
        Assert.Equal(expected: 0.041667, actual: cues[index: 0].StartSeconds, precision: 6);
        Assert.Equal(expected: 2.083333, actual: cues[index: 0].EndSeconds, precision: 6);
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
                    .GetMethod(name: "ParseOcrOutput", bindingAttr: BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(obj: null, parameters: [content])!;
    }
}
