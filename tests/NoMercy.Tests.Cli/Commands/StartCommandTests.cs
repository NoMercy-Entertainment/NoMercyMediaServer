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
/// REQUIREMENT: <c>start</c> must never attempt to launch a second server
/// process when one is already reachable — it must short-circuit as soon as
/// <c>/manage/status</c> answers, printing "Server is already running." and
/// exiting 0. Whether the server is unreachable via an error status or a
/// transport-level exception, both must be treated identically as "not
/// running" (never let a transport exception escape uncaught).
///
/// The "not running" continuation (probe the filesystem for a server binary,
/// then <c>Process.Start()</c> it) is covered separately, at the private
/// helper level, in <see cref="StartCommandStartInfoTests"/> — invoking the
/// full action for that branch would, on this checkout, actually spawn the
/// real NoMercy.Service process (both Debug and Release builds already exist
/// on disk), which a unit test must never do. See the coverage report for the
/// itemized residue this leaves in <c>StartCommand.Create()</c>'s action body
/// (the <c>Process.Start()</c> call and its surrounding null/try-catch).
/// </summary>
[Trait("Category", "Unit")]
public sealed class StartCommandTests
{
    private static async Task<int> RunAlreadyRunningAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(StartCommand.Create(pipeOption, factory));
        return await root.Parse(["start"]).InvokeAsync();
    }

    [Fact]
    public async Task Start_ServerAlreadyRunning_PrintsMessage_AndReturnsSuccess_WithoutLaunching()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatusResponse { Status = "running" });

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAlreadyRunningAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Server is already running.");
    }

    [Fact]
    public async Task IsServerRunning_ClientReturnsStatus_ReturnsTrue()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatusResponse { Status = "running" });

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        bool? result = await PrivateReflection.InvokeStaticAsync<bool>(
            typeof(StartCommand),
            "IsServerRunning",
            factory.Object,
            null,
            CancellationToken.None
        );

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsServerRunning_ClientReturnsNull_ReturnsFalse()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StatusResponse?)null);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        bool? result = await PrivateReflection.InvokeStaticAsync<bool>(
            typeof(StartCommand),
            "IsServerRunning",
            factory.Object,
            null,
            CancellationToken.None
        );

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsServerRunning_ClientThrows_ReturnsFalse_WithoutPropagatingException()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<StatusResponse>(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        bool? result = await PrivateReflection.InvokeStaticAsync<bool>(
            typeof(StartCommand),
            "IsServerRunning",
            factory.Object,
            null,
            CancellationToken.None
        );

        result.Should().BeFalse();
    }
}
