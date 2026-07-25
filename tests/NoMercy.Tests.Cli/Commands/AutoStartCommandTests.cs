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
using NoMercy.Cli;
using NoMercy.Cli.Commands;
using NoMercy.Tests.Cli.Support;
using NoMercy.Tests.Common.Ipc;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>autostart status/enable/disable</c> must report the exact
/// enabled/disabled state the server returns (never inverted), must fail
/// distinctly when unreachable, and <c>enable</c>/<c>disable</c> must each send
/// the correctly-shaped <c>{ "enabled": bool }</c> body. The response DTO for
/// this command is a private nested type, so it cannot be named from the test
/// assembly to set up a mock — these run against a real
/// <see cref="FakeManagementPipeServer"/> instead, which exercises the exact
/// same JSON contract the production server implements.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AutoStartCommandTests
{
    private static async Task<(int ExitCode, string Request)> RunAsync(params string[] subArgs)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(AutoStartCommand.Create(pipeOption, new CliClientFactory()));

        FakeManagementPipeServer server = new();
        Task<string> serverTask = server.RunOnceAsync(stream =>
            FakeManagementPipeServer.WriteResponseAsync(stream, 200, "OK", """{"status":"ok"}""")
        );

        int exitCode = await root.Parse(["--pipe", server.PipeName, "autostart", .. subArgs])
            .InvokeAsync();

        string capturedRequest = await serverTask;
        return (exitCode, capturedRequest);
    }

    [Fact]
    public async Task Status_ServerReportsUnreachableViaErrorStatus_PrintsError_AndReturnsServerError()
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(AutoStartCommand.Create(pipeOption, new CliClientFactory()));

        FakeManagementPipeServer server = new();
        Task<string> serverTask = server.RunOnceAsync(stream =>
            FakeManagementPipeServer.WriteResponseAsync(stream, 500, "Internal Server Error", "")
        );

        using ConsoleCapture console = new();
        int exitCode = await root.Parse(["--pipe", server.PipeName, "autostart", "status"])
            .InvokeAsync();

        await serverTask;
        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Could not retrieve autostart status.");
    }

    [Theory]
    [InlineData([true, "enabled"])]
    [InlineData([false, "disabled"])]
    public async Task Status_ServerReachable_PrintsReportedState(bool enabled, string expectedWord)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(AutoStartCommand.Create(pipeOption, new CliClientFactory()));

        FakeManagementPipeServer server = new();
        Task<string> serverTask = server.RunOnceAsync(stream =>
            FakeManagementPipeServer.WriteResponseAsync(
                stream,
                200,
                "OK",
                $"{{\"enabled\":{(enabled ? "true" : "false")}}}"
            )
        );

        using ConsoleCapture console = new();
        int exitCode = await root.Parse(["--pipe", server.PipeName, "autostart", "status"])
            .InvokeAsync();

        await serverTask;
        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain($"Autostart:    {expectedWord}");
    }

    [Fact]
    public async Task Enable_SendsEnabledTrue_AndReturnsSuccess()
    {
        using ConsoleCapture console = new();
        (int exitCode, string request) = await RunAsync("enable");

        exitCode.Should().Be((int)ExitCode.Success);
        request.Should().StartWith("POST /manage/autostart");
        request.Should().Contain("\"enabled\":true");
        console.Out.Should().Contain("Autostart enabled.");
    }

    [Fact]
    public async Task Disable_SendsEnabledFalse_AndReturnsSuccess()
    {
        using ConsoleCapture console = new();
        (int exitCode, string request) = await RunAsync("disable");

        exitCode.Should().Be((int)ExitCode.Success);
        request.Should().Contain("\"enabled\":false");
        console.Out.Should().Contain("Autostart disabled.");
    }

    [Fact]
    public async Task Enable_NotAcknowledged_ReturnsServerError_WithoutSuccessMessage()
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(AutoStartCommand.Create(pipeOption, new CliClientFactory()));

        FakeManagementPipeServer server = new();
        Task<string> serverTask = server.RunOnceAsync(stream =>
            FakeManagementPipeServer.WriteResponseAsync(stream, 400, "Bad Request", "")
        );

        using ConsoleCapture console = new();
        int exitCode = await root.Parse(["--pipe", server.PipeName, "autostart", "enable"])
            .InvokeAsync();

        await serverTask;
        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Out.Should().NotContain("Autostart enabled.");
    }
}
