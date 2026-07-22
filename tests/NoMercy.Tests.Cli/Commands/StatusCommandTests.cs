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
/// REQUIREMENT: <c>status</c> must report a connection failure distinctly
/// (stderr + <see cref="ExitCode.ServerError"/>, not a stack trace or a silent
/// zero exit) from a normal status report, and the "Mode: Development" line
/// must appear only when the server actually reports dev mode — never
/// unconditionally.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class StatusCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory, params string[] args)
    {
        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        RootCommand root = new(description: "test");
        root.Options.Add(item: pipeOption);
        root.Subcommands.Add(item: StatusCommand.Create(pipeOption: pipeOption, clientFactory: factory));
        return await root.Parse(args: args).InvokeAsync();
    }

    [Fact]
    public async Task Status_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (StatusResponse?)null);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object, args: "status");

        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Error.Should().Contain(expected: "Could not connect to server.");
    }

    [Fact]
    public async Task Status_RunningServer_PrintsFields_WithoutDevLine()
    {
        StatusResponse response = new()
        {
            Status = "running",
            ServerName = "nomercy-prod",
            Version = "1.2.3",
            Platform = "linux",
            Architecture = "x64",
            Os = "Ubuntu 24.04",
            UptimeSeconds = 3661,
            StartTime = new DateTime(year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc),
            IsDev = false,
        };

        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: response);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object, args: "status");

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        console.Out.Should().Contain(expected: "Status:       running");
        console.Out.Should().Contain(expected: "Server:       nomercy-prod");
        console.Out.Should().Contain(expected: "Version:      1.2.3");
        console.Out.Should().Contain(expected: "Platform:     linux (x64)");
        console.Out.Should().Contain(expected: "Uptime:       1h 1m");
        console.Out.Should().NotContain(unexpected: "Mode:");
    }

    [Fact]
    public async Task Status_DevServer_PrintsDevelopmentModeLine()
    {
        StatusResponse response = new()
        {
            Status = "running",
            ServerName = "nomercy-dev",
            Version = "0.1.404",
            Platform = "win",
            Architecture = "x64",
            Os = "Windows 10",
            UptimeSeconds = 30,
            StartTime = DateTime.UtcNow,
            IsDev = true,
        };

        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: response);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object, args: "status");

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        console.Out.Should().Contain(expected: "Mode:         Development");
    }

    [Fact]
    public async Task Status_PassesGlobalPipeOption_ToClientFactory()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (StatusResponse?)null);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture _ = new();
        await RunAsync(factory: factory.Object, args: ["--pipe", "custom-pipe", "status"]);

        factory.Verify(expression: f => f.Create("custom-pipe"), times: Times.Once);
    }
}
