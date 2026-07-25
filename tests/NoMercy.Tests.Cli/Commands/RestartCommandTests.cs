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
using NoMercy.Tests.Cli.Support;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>restart</c> must report success only when the server
/// acknowledged the restart request, and must never report success on a
/// failed POST.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RestartCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(RestartCommand.Create(pipeOption, factory));
        return await root.Parse(["restart"]).InvokeAsync();
    }

    [Fact]
    public async Task Restart_Acknowledged_PrintsRequested_AndReturnsSuccess()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.PostAsync(ApiRoutes.Restart, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Server restart requested.");
    }

    [Fact]
    public async Task Restart_NotAcknowledged_ReturnsServerError_WithoutSuccessMessage()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.PostAsync(ApiRoutes.Restart, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Out.Should().NotContain("requested");
    }
}
