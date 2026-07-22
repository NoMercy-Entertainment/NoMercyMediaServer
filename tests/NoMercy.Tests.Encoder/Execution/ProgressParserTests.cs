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

using NoMercy.Encoder.Execution;

namespace NoMercy.Tests.Encoder.Execution;

public class ProgressParserTests
{
    [Fact]
    public void Parse_CompleteBlock_ReturnsSnapshot()
    {
        ProgressParser parser = new();

        parser.FeedLine(line: "frame=1234").Should().BeNull();
        parser.FeedLine(line: "fps=59.8").Should().BeNull();
        parser.FeedLine(line: "bitrate=8234.5kbits/s").Should().BeNull();
        parser.FeedLine(line: "total_size=12345678").Should().BeNull();
        parser.FeedLine(line: "out_time_us=60000000").Should().BeNull();
        parser.FeedLine(line: "speed=2.50x").Should().BeNull();

        FfmpegProgressSnapshot? snapshot = parser.FeedLine(line: "progress=continue");

        snapshot.Should().NotBeNull();
        snapshot!.Frame.Should().Be(expected: 1234);
        snapshot.Fps.Should().BeApproximately(expectedValue: 59.8, precision: 0.01);
        snapshot.BitrateKbps.Should().BeApproximately(expectedValue: 8234.5, precision: 0.1);
        snapshot.TotalSizeBytes.Should().Be(expected: 12345678);
        snapshot.OutTime.Should().Be(expected: TimeSpan.FromSeconds(seconds: 60));
        snapshot.Speed.Should().BeApproximately(expectedValue: 2.5, precision: 0.01);
        snapshot.IsEnd.Should().BeFalse();
    }

    [Fact]
    public void Parse_EndProgress_IsEndTrue()
    {
        ProgressParser parser = new();
        parser.FeedLine(line: "frame=100");
        parser.FeedLine(line: "fps=30.0");
        parser.FeedLine(line: "speed=1.0x");
        parser.FeedLine(line: "out_time_us=5000000");

        FfmpegProgressSnapshot? snapshot = parser.FeedLine(line: "progress=end");

        snapshot.Should().NotBeNull();
        snapshot!.IsEnd.Should().BeTrue();
    }

    [Fact]
    public void Parse_NaBitrate_ReturnsNull()
    {
        ProgressParser parser = new();
        parser.FeedLine(line: "bitrate=N/A");
        parser.FeedLine(line: "speed=N/A");

        FfmpegProgressSnapshot? snapshot = parser.FeedLine(line: "progress=continue");

        snapshot.Should().NotBeNull();
        snapshot!.BitrateKbps.Should().BeNull();
        snapshot.Speed.Should().Be(expected: 0);
    }

    [Fact]
    public void Parse_EmptyLine_ReturnsNull()
    {
        ProgressParser parser = new();
        parser.FeedLine(line: "").Should().BeNull();
        parser.FeedLine(line: "   ").Should().BeNull();
    }

    [Fact]
    public void Parse_MalformedLine_ReturnsNull()
    {
        ProgressParser parser = new();
        parser.FeedLine(line: "no equals sign here").Should().BeNull();
    }

    [Fact]
    public void Parse_MultipleBlocks_ReturnsMultipleSnapshots()
    {
        ProgressParser parser = new();

        parser.FeedLine(line: "frame=100");
        parser.FeedLine(line: "speed=2.0x");
        parser.FeedLine(line: "out_time_us=5000000");
        FfmpegProgressSnapshot? first = parser.FeedLine(line: "progress=continue");

        parser.FeedLine(line: "frame=200");
        parser.FeedLine(line: "speed=2.5x");
        parser.FeedLine(line: "out_time_us=10000000");
        FfmpegProgressSnapshot? second = parser.FeedLine(line: "progress=continue");

        first.Should().NotBeNull();
        first!.Frame.Should().Be(expected: 100);
        second.Should().NotBeNull();
        second!.Frame.Should().Be(expected: 200);
        second.Speed.Should().BeApproximately(expectedValue: 2.5, precision: 0.01);
    }

    [Fact]
    public void Parse_SpeedFormats()
    {
        ProgressParser parser = new();

        parser.FeedLine(line: "speed=0.5x");
        FfmpegProgressSnapshot? slow = parser.FeedLine(line: "progress=continue");
        slow!.Speed.Should().BeApproximately(expectedValue: 0.5, precision: 0.01);

        parser.FeedLine(line: "speed=10.2x");
        FfmpegProgressSnapshot? fast = parser.FeedLine(line: "progress=continue");
        fast!.Speed.Should().BeApproximately(expectedValue: 10.2, precision: 0.1);
    }

    [Fact]
    public void Parse_OutTimeConvertsCorrectly()
    {
        ProgressParser parser = new();
        parser.FeedLine(line: "out_time_us=7200000000"); // 7200 seconds = 2 hours
        FfmpegProgressSnapshot? snapshot = parser.FeedLine(line: "progress=continue");

        snapshot!.OutTime.Should().Be(expected: TimeSpan.FromHours(hours: 2));
    }
}
