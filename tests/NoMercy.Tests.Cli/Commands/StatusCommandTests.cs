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
[Trait("Category", "Unit")]
public sealed class StatusCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory, params string[] args)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(StatusCommand.Create(pipeOption, factory));
        return await root.Parse(args).InvokeAsync();
    }

    [Fact]
    public async Task Status_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StatusResponse?)null);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object, "status");

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Could not connect to server.");
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
            StartTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsDev = false,
        };

        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object, "status");

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Status:       running");
        console.Out.Should().Contain("Server:       nomercy-prod");
        console.Out.Should().Contain("Version:      1.2.3");
        console.Out.Should().Contain("Platform:     linux (x64)");
        console.Out.Should().Contain("Uptime:       1h 1m");
        console.Out.Should().NotContain("Mode:");
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
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object, "status");

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Mode:         Development");
    }

    [Fact]
    public async Task Status_PassesGlobalPipeOption_ToClientFactory()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StatusResponse?)null);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture _ = new();
        await RunAsync(factory.Object, "--pipe", "custom-pipe", "status");

        factory.Verify(f => f.Create("custom-pipe"), Times.Once);
    }
}
