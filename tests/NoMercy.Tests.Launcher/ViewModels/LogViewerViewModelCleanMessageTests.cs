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
using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;
using Xunit;

namespace NoMercy.Tests.Launcher.ViewModels;

/// <summary>
/// <c>LogViewerViewModel.CleanMessage</c> is <c>private static</c> - it only
/// ever runs today from the network/disk log-loading paths (RefreshLogsAsync,
/// LoadLogsFromDiskAsync, the SSE stream callback), all of which require a
/// live management pipe or on-disk log files to exercise end-to-end. Reflection
/// invokes the real method directly (not a reimplementation of it) to pin the
/// exact ANSI-stripping / re-escaping contract that keeps double-serialized
/// log lines readable in the viewer. <c>MatchesFilter</c> is likewise
/// private and only reachable from the live SSE callback; it is reflected the
/// same way to pin its (message-or-type, case-insensitive) contract, which is
/// deliberately narrower than <c>ApplyFilter</c>'s (it never re-checks level -
/// the streaming caller already filtered on level before calling it).
/// </summary>
public sealed class LogViewerViewModelCleanMessageTests
{
    private static readonly MethodInfo CleanMessageMethod = typeof(LogViewerViewModel).GetMethod(
        "CleanMessage",
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly MethodInfo MatchesFilterMethod = typeof(LogViewerViewModel).GetMethod(
        "MatchesFilter",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    /// <summary>ASCII ESC control code, built numerically so no literal escape
    /// byte lives in this source file.</summary>
    private static readonly char EscByte = Convert.ToChar(27);

    private static LogEntryResponse CleanMessage(string rawMessage)
    {
        LogEntryResponse entry = new() { Message = rawMessage };
        CleanMessageMethod.Invoke(null, [entry]);
        return entry;
    }

    private static bool MatchesFilter(LogViewerViewModel viewModel, LogEntryResponse entry) =>
        (bool)MatchesFilterMethod.Invoke(viewModel, [entry])!;

    [Fact]
    public void CleanMessage_SurroundingDoubleQuotes_AreStripped()
    {
        LogEntryResponse entry = CleanMessage("\"hello world\"");

        entry.Message.Should().Be("hello world");
    }

    [Fact]
    public void CleanMessage_RealEscByteAnsiColorEscape_IsRemoved()
    {
        // The real ESC (0x1B) control byte, exactly as a terminal-colored
        // console sink would emit it.
        string ansiGreen = EscByte + "[32mgreen text" + EscByte + "[0m";

        LogEntryResponse entry = CleanMessage(ansiGreen);

        entry.Message.Should().Be("green text");
    }

    [Fact]
    public void CleanMessage_LiteralBackslashUAnsiEscapeText_IsRemoved()
    {
        // Double-serialized logs can carry the escape as the literal six
        // characters backslash-u-0-0-1-b instead of the real ESC byte - the
        // regex's second alternative exists specifically for this shape.
        string literalAnsiRed = "\\u001b[31mred\\u001b[0m";

        LogEntryResponse entry = CleanMessage(literalAnsiRed);

        entry.Message.Should().Be("red");
    }

    [Fact]
    public void CleanMessage_EscapedNewlineTabAndCarriageReturn_AreUnescaped()
    {
        string doubleEscaped = "line1\\nline2\\tindented\\rreturn";

        LogEntryResponse entry = CleanMessage(doubleEscaped);

        entry.Message.Should().Be("line1\nline2\tindented\rreturn");
    }

    [Fact]
    public void CleanMessage_EscapedDoubleQuote_IsUnescaped()
    {
        string doubleEscaped = "say \\\"hi\\\"";

        LogEntryResponse entry = CleanMessage(doubleEscaped);

        entry.Message.Should().Be("say \"hi\"");
    }

    [Fact]
    public void CleanMessage_DoubledBackslash_IsCollapsedToSingle()
    {
        string doubleEscaped = "path C:\\\\Games\\\\NoMercy";

        LogEntryResponse entry = CleanMessage(doubleEscaped);

        entry.Message.Should().Be("path C:\\Games\\NoMercy");
    }

    [Fact]
    public void CleanMessage_PlainMessage_IsUnchanged()
    {
        LogEntryResponse entry = CleanMessage("server listening on port 7626");

        entry.Message.Should().Be("server listening on port 7626");
    }

    [Fact]
    public void CleanMessage_SingleCharacterMessage_DoesNotStripAsQuotedPair()
    {
        // message.Length >= 2 guards the quote-stripping branch - a
        // single-character message must survive unchanged rather than being
        // treated as an (impossible) matching open+close quote pair.
        LogEntryResponse entry = CleanMessage("\"");

        entry.Message.Should().Be("\"");
    }

    [Fact]
    public void MatchesFilter_EmptySearchText_MatchesEverything()
    {
        LogViewerViewModel viewModel = new(new ServerConnection());
        LogEntryResponse entry = new() { Type = "Server", Message = "anything" };

        MatchesFilter(viewModel, entry).Should().BeTrue();
    }

    [Fact]
    public void MatchesFilter_SearchTextInMessage_CaseInsensitiveMatch()
    {
        LogViewerViewModel viewModel = new(new ServerConnection()) { SearchText = "LISTENING" };
        LogEntryResponse entry = new() { Type = "Server", Message = "now listening on 7626" };

        MatchesFilter(viewModel, entry).Should().BeTrue();
    }

    [Fact]
    public void MatchesFilter_SearchTextInTypeOnly_StillMatches()
    {
        LogViewerViewModel viewModel = new(new ServerConnection()) { SearchText = "encoder" };
        LogEntryResponse entry = new() { Type = "Encoder", Message = "totally unrelated" };

        MatchesFilter(viewModel, entry).Should().BeTrue();
    }

    [Fact]
    public void MatchesFilter_SearchTextInNeither_DoesNotMatch()
    {
        LogViewerViewModel viewModel = new(new ServerConnection()) { SearchText = "database" };
        LogEntryResponse entry = new() { Type = "Encoder", Message = "totally unrelated" };

        MatchesFilter(viewModel, entry).Should().BeFalse();
    }
}
