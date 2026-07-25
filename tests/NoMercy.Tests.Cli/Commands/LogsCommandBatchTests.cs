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

using System.CommandLine;
using Moq;
using NoMercy.Cli;
using NoMercy.Cli.Commands;
using NoMercy.Cli.Models;
using NoMercy.Tests.Cli.Support;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>logs</c> (without <c>--follow</c>) fetches one batch, fails
/// distinctly when unreachable, and prints every entry with its message
/// "cleaned" (double-serialization quotes/escapes and ANSI codes stripped) —
/// and must print a visible session-restart separator whenever an entry's
/// timestamp goes backwards relative to the previous one, since that is the
/// CLI's only signal that the server process restarted mid-log.
///
/// The static <c>_lastEntryTime</c> field this relies on is reset before every
/// test via reflection so results never depend on execution order.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LogsCommandBatchTests
{
    public LogsCommandBatchTests()
    {
        PrivateReflection.ResetStaticField(
            typeof(LogsCommand),
            "_lastEntryTime",
            DateTime.MinValue
        );
    }

    private static async Task<int> RunAsync(ICliClientFactory factory, params string[] extraArgs)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(LogsCommand.Create(pipeOption, factory));
        return await root.Parse(["logs", .. extraArgs]).InvokeAsync();
    }

    private static Mock<ICliClientFactory> FactoryReturning(
        List<LogEntryResponse>? logs,
        out Mock<ICliClient> client
    )
    {
        client = new Mock<ICliClient>();
        client
            .Setup(c =>
                c.GetAsync<List<LogEntryResponse>>(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(logs);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);
        return factory;
    }

    [Fact]
    public async Task Logs_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClientFactory> factory = FactoryReturning(null, out _);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Could not connect to server.");
    }

    [Fact]
    public async Task Logs_RequestsWithTailLevelAndTypeQueryParameters()
    {
        Mock<ICliClientFactory> factory = FactoryReturning([], out Mock<ICliClient> client);

        using ConsoleCapture _ = new();
        await RunAsync(factory.Object, ["--tail", "50", "--level", "Error", "--type", "App"]);

        client.Verify(
            c =>
                c.GetAsync<List<LogEntryResponse>>(
                    "/manage/logs?tail=50&levels=Error&types=App",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Logs_StripsDoubleSerializationArtifacts_FromMessage()
    {
        List<LogEntryResponse> entries =
        [
            new()
            {
                Type = "App",
                Level = "Information",
                Color = "",
                Time = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                Message = "\"line one\\nline two with \\\"quotes\\\"\"",
            },
        ];

        Mock<ICliClientFactory> factory = FactoryReturning(entries, out _);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("line one\nline two with \"quotes\"");
        console.Out.Should().NotContain("\\n");
        console.Out.Should().NotContain("\\\"");
    }

    [Fact]
    public async Task Logs_StripsAnsiEscapeCodes_FromMessage()
    {
        List<LogEntryResponse> entries =
        [
            new()
            {
                Type = "App",
                Level = "Information",
                Color = "",
                Time = DateTime.UtcNow,
                Message = "[31mred text[0m",
            },
        ];

        Mock<ICliClientFactory> factory = FactoryReturning(entries, out _);

        using ConsoleCapture console = new();
        await RunAsync(factory.Object);

        // "31m" is the SGR code from the injected ANSI escape in the fixture
        // above; it must be gone after cleaning. The timestamp/type columns are
        // separately, always colored via a different Pastel call, so asserting
        // "no ESC/bracket anywhere in the line" would fail regardless of whether
        // CleanMessage did its job -- this asserts on the specific stripped
        // sequence instead.
        console.Out.Should().Contain("| red text");
        console.Out.Should().NotContain("31m");
    }

    [Fact]
    public async Task Logs_TimestampGoesBackwards_PrintsSessionRestartSeparator()
    {
        List<LogEntryResponse> entries =
        [
            new()
            {
                Type = "App",
                Level = "Information",
                Color = "",
                Time = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Message = "before restart",
            },
            new()
            {
                Type = "App",
                Level = "Information",
                Color = "",
                Time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Message = "after restart",
            },
        ];

        Mock<ICliClientFactory> factory = FactoryReturning(entries, out _);

        using ConsoleCapture console = new();
        await RunAsync(factory.Object);

        console.Out.Should().Contain("Server Restart");
    }

    [Fact]
    public async Task Logs_MonotonicTimestamps_NeverPrintsSessionRestartSeparator()
    {
        List<LogEntryResponse> entries =
        [
            new()
            {
                Type = "App",
                Level = "Information",
                Color = "",
                Time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Message = "first",
            },
            new()
            {
                Type = "App",
                Level = "Information",
                Color = "",
                Time = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc),
                Message = "second",
            },
        ];

        Mock<ICliClientFactory> factory = FactoryReturning(entries, out _);

        using ConsoleCapture console = new();
        await RunAsync(factory.Object);

        console.Out.Should().NotContain("Server Restart");
    }

    [Fact]
    public async Task Logs_EntryWithColor_StillPrintsMessage()
    {
        List<LogEntryResponse> entries =
        [
            new()
            {
                Type = "Encoder",
                Level = "Error",
                // The server always sends a "#RRGGBB" hex string (see
                // NoMercyLoggerProvider's category.DarkHex) — Pastel's
                // string-color overload only accepts that format, so a named
                // color like "Red" would throw.
                Color = "#FF0000",
                Time = DateTime.UtcNow,
                Message = "encode failed",
            },
        ];

        Mock<ICliClientFactory> factory = FactoryReturning(entries, out _);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("encode failed");
    }
}
