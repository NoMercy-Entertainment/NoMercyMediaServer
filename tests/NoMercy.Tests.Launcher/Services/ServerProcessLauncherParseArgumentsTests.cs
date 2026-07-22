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

using NoMercy.Launcher.Services;
using Xunit;

namespace NoMercy.Tests.Launcher.Services;

/// <summary>
/// REQUIREMENT: <see cref="ServerProcessLauncher.ParseArguments"/> is the only
/// thing standing between a user-typed startup-arguments string in the
/// Settings tab and a real <c>ProcessStartInfo.ArgumentList</c> — it must split
/// on whitespace, honor double-quoted segments (so a path with a space in it
/// survives as ONE argument), and never throw on malformed input (an
/// unterminated quote must still produce something usable rather than crash
/// server startup).
/// </summary>
public sealed class ServerProcessLauncherParseArgumentsTests
{
    [Fact]
    public void ParseArguments_SimpleFlags_SplitsOnWhitespace()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "--dev --port 7626");

        result.Should().Equal(expected: ["--dev", "--port", "7626"]);
    }

    [Fact]
    public void ParseArguments_EmptyString_ReturnsEmptyList()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseArguments_OnlyWhitespace_ReturnsEmptyList()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "    \t  ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseArguments_QuotedSegmentWithSpaces_KeptAsSingleArgument()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(
            input: "--library \"D:\\My Movies\" --dev"
        );

        result.Should().Equal(expected: ["--library", "D:\\My Movies", "--dev"]);
    }

    [Fact]
    public void ParseArguments_MultipleSpacesBetweenArguments_CollapsedToOneSeparator()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "--dev    --port   7626");

        result.Should().Equal(expected: ["--dev", "--port", "7626"]);
    }

    [Fact]
    public void ParseArguments_UnterminatedQuote_ReturnsRemainderAsOneArgument()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "--library \"D:\\Unterminated");

        result.Should().Equal(expected: ["--library", "D:\\Unterminated"]);
    }

    [Fact]
    public void ParseArguments_EmptyQuotedSegment_ProducesEmptyStringArgument()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "--name \"\" --dev");

        result.Should().Equal(expected: ["--name", "", "--dev"]);
    }

    [Fact]
    public void ParseArguments_AdjacentQuotedSegments_ProducesTwoSeparateArguments()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "\"first\"\"second\"");

        result.Should().Equal(expected: ["first", "second"]);
    }

    [Fact]
    public void ParseArguments_LeadingAndTrailingWhitespace_Trimmed()
    {
        List<string> result = ServerProcessLauncher.ParseArguments(input: "   --dev   ");

        result.Should().Equal(expected: "--dev");
    }
}
