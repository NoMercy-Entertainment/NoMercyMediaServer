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
[Trait(name: "Category", value: "Unit")]
public sealed class RestartCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        RootCommand root = new(description: "test");
        root.Options.Add(item: pipeOption);
        root.Subcommands.Add(item: RestartCommand.Create(pipeOption: pipeOption, clientFactory: factory));
        return await root.Parse(args: ["restart"]).InvokeAsync();
    }

    [Fact]
    public async Task Restart_Acknowledged_PrintsRequested_AndReturnsSuccess()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.PostAsync(ApiRoutes.Restart, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object);

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        console.Out.Should().Contain(expected: "Server restart requested.");
    }

    [Fact]
    public async Task Restart_NotAcknowledged_ReturnsServerError_WithoutSuccessMessage()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.PostAsync(ApiRoutes.Restart, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: false);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object);

        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Out.Should().NotContain(unexpected: "requested");
    }
}
