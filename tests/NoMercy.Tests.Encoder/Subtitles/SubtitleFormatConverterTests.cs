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

using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

public class SubtitleFormatConverterTests
{
    private const string SampleSrt =
        "1\r\n00:00:01,000 --> 00:00:04,000\r\nHello, world!\r\n\r\n2\r\n00:00:05,500 --> 00:00:07,250\r\nSecond line.\r\n";

    [Fact]
    public void SrtToVtt_PrependsWebVttHeader()
    {
        string vtt = SubtitleFormatConverter.SrtToVtt(srtContent: SampleSrt);

        vtt.Should().StartWith(expected: "WEBVTT\n\n");
    }

    [Fact]
    public void SrtToVtt_RewritesCommaMillisecondSeparatorToPeriod()
    {
        string vtt = SubtitleFormatConverter.SrtToVtt(srtContent: SampleSrt);

        vtt.Should().Contain(expected: "00:00:01.000 --> 00:00:04.000");
        vtt.Should().Contain(expected: "00:00:05.500 --> 00:00:07.250");
        vtt.Should().NotContain(unexpected: ",000");
        vtt.Should().NotContain(unexpected: ",500");
    }

    [Fact]
    public void SrtToVtt_PreservesCueNumbersAndPayloadText()
    {
        string vtt = SubtitleFormatConverter.SrtToVtt(srtContent: SampleSrt);

        vtt.Should().Contain(expected: "Hello, world!");
        vtt.Should().Contain(expected: "Second line.");
        // Cue index lines pass through untouched.
        vtt.Should().Contain(expected: "\n1\n");
        vtt.Should().Contain(expected: "\n2\n");
    }

    [Fact]
    public void SrtToVtt_NormalizesCrLfLineEndingsToLf()
    {
        string vtt = SubtitleFormatConverter.SrtToVtt(srtContent: SampleSrt);

        vtt.Should().NotContain(unexpected: "\r\n");
    }

    [Fact]
    public void SrtToVtt_StripsLeadingByteOrderMark()
    {
        string srtWithBom = "﻿" + SampleSrt;

        string vtt = SubtitleFormatConverter.SrtToVtt(srtContent: srtWithBom);

        vtt.Should().StartWith(expected: "WEBVTT");
        vtt.Should().NotContain(unexpected: "﻿");
    }

    [Fact]
    public void SrtToVtt_DoesNotCorruptCommasInsidePayloadText()
    {
        const string srt = "1\n00:00:01,000 --> 00:00:02,000\nHi, there, friend.\n";

        string vtt = SubtitleFormatConverter.SrtToVtt(srtContent: srt);

        vtt.Should().Contain(expected: "Hi, there, friend.");
    }
}
