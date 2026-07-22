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
        name: "CleanMessage",
        bindingAttr: BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly MethodInfo MatchesFilterMethod = typeof(LogViewerViewModel).GetMethod(
        name: "MatchesFilter",
        bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    /// <summary>ASCII ESC control code, built numerically so no literal escape
    /// byte lives in this source file.</summary>
    private static readonly char EscByte = Convert.ToChar(value: 27);

    private static LogEntryResponse CleanMessage(string rawMessage)
    {
        LogEntryResponse entry = new() { Message = rawMessage };
        CleanMessageMethod.Invoke(obj: null, parameters: [entry]);
        return entry;
    }

    private static bool MatchesFilter(LogViewerViewModel viewModel, LogEntryResponse entry) =>
        (bool)MatchesFilterMethod.Invoke(obj: viewModel, parameters: [entry])!;

    [Fact]
    public void CleanMessage_SurroundingDoubleQuotes_AreStripped()
    {
        LogEntryResponse entry = CleanMessage(rawMessage: "\"hello world\"");

        entry.Message.Should().Be(expected: "hello world");
    }

    [Fact]
    public void CleanMessage_RealEscByteAnsiColorEscape_IsRemoved()
    {
        // The real ESC (0x1B) control byte, exactly as a terminal-colored
        // console sink would emit it.
        string ansiGreen = EscByte + "[32mgreen text" + EscByte + "[0m";

        LogEntryResponse entry = CleanMessage(rawMessage: ansiGreen);

        entry.Message.Should().Be(expected: "green text");
    }

    [Fact]
    public void CleanMessage_LiteralBackslashUAnsiEscapeText_IsRemoved()
    {
        // Double-serialized logs can carry the escape as the literal six
        // characters backslash-u-0-0-1-b instead of the real ESC byte - the
        // regex's second alternative exists specifically for this shape.
        string literalAnsiRed = "\\u001b[31mred\\u001b[0m";

        LogEntryResponse entry = CleanMessage(rawMessage: literalAnsiRed);

        entry.Message.Should().Be(expected: "red");
    }

    [Fact]
    public void CleanMessage_EscapedNewlineTabAndCarriageReturn_AreUnescaped()
    {
        string doubleEscaped = "line1\\nline2\\tindented\\rreturn";

        LogEntryResponse entry = CleanMessage(rawMessage: doubleEscaped);

        entry.Message.Should().Be(expected: "line1\nline2\tindented\rreturn");
    }

    [Fact]
    public void CleanMessage_EscapedDoubleQuote_IsUnescaped()
    {
        string doubleEscaped = "say \\\"hi\\\"";

        LogEntryResponse entry = CleanMessage(rawMessage: doubleEscaped);

        entry.Message.Should().Be(expected: "say \"hi\"");
    }

    [Fact]
    public void CleanMessage_DoubledBackslash_IsCollapsedToSingle()
    {
        string doubleEscaped = "path C:\\\\Games\\\\NoMercy";

        LogEntryResponse entry = CleanMessage(rawMessage: doubleEscaped);

        entry.Message.Should().Be(expected: "path C:\\Games\\NoMercy");
    }

    [Fact]
    public void CleanMessage_PlainMessage_IsUnchanged()
    {
        LogEntryResponse entry = CleanMessage(rawMessage: "server listening on port 7626");

        entry.Message.Should().Be(expected: "server listening on port 7626");
    }

    [Fact]
    public void CleanMessage_SingleCharacterMessage_DoesNotStripAsQuotedPair()
    {
        // message.Length >= 2 guards the quote-stripping branch - a
        // single-character message must survive unchanged rather than being
        // treated as an (impossible) matching open+close quote pair.
        LogEntryResponse entry = CleanMessage(rawMessage: "\"");

        entry.Message.Should().Be(expected: "\"");
    }

    [Fact]
    public void MatchesFilter_EmptySearchText_MatchesEverything()
    {
        LogViewerViewModel viewModel = new(serverConnection: new ServerConnection());
        LogEntryResponse entry = new() { Type = "Server", Message = "anything" };

        MatchesFilter(viewModel: viewModel, entry: entry).Should().BeTrue();
    }

    [Fact]
    public void MatchesFilter_SearchTextInMessage_CaseInsensitiveMatch()
    {
        LogViewerViewModel viewModel = new(serverConnection: new ServerConnection()) { SearchText = "LISTENING" };
        LogEntryResponse entry = new() { Type = "Server", Message = "now listening on 7626" };

        MatchesFilter(viewModel: viewModel, entry: entry).Should().BeTrue();
    }

    [Fact]
    public void MatchesFilter_SearchTextInTypeOnly_StillMatches()
    {
        LogViewerViewModel viewModel = new(serverConnection: new ServerConnection()) { SearchText = "encoder" };
        LogEntryResponse entry = new() { Type = "Encoder", Message = "totally unrelated" };

        MatchesFilter(viewModel: viewModel, entry: entry).Should().BeTrue();
    }

    [Fact]
    public void MatchesFilter_SearchTextInNeither_DoesNotMatch()
    {
        LogViewerViewModel viewModel = new(serverConnection: new ServerConnection()) { SearchText = "database" };
        LogEntryResponse entry = new() { Type = "Encoder", Message = "totally unrelated" };

        MatchesFilter(viewModel: viewModel, entry: entry).Should().BeFalse();
    }
}
