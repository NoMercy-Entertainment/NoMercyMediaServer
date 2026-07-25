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
/// REQUIREMENT: <c>plugin list</c> must distinguish three states a user can
/// actually hit — unreachable server, zero plugins installed, and a populated
/// table — with a distinct message/exit code for each, never conflating "no
/// plugins" with "couldn't connect".
/// </summary>
[Trait("Category", "Unit")]
public sealed class PluginCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(PluginCommand.Create(pipeOption, factory));
        return await root.Parse(["plugin", "list"]).InvokeAsync();
    }

    private static Mock<ICliClientFactory> FactoryReturning(List<PluginResponse>? plugins)
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.GetAsync<List<PluginResponse>>(ApiRoutes.Plugins, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(plugins);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);
        return factory;
    }

    [Fact]
    public async Task List_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClientFactory> factory = FactoryReturning(null);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Could not connect to server.");
    }

    [Fact]
    public async Task List_NoPlugins_PrintsEmptyMessage_AndReturnsSuccess()
    {
        Mock<ICliClientFactory> factory = FactoryReturning([]);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("No plugins installed.");
    }

    [Fact]
    public async Task List_WithPlugins_PrintsTable_AndReturnsSuccess()
    {
        List<PluginResponse> plugins =
        [
            new()
            {
                Id = "echo",
                Name = "Echo Sample",
                Version = "1.0.0",
                Status = "enabled",
                Author = "NoMercy",
            },
        ];

        Mock<ICliClientFactory> factory = FactoryReturning(plugins);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Echo Sample");
        console.Out.Should().Contain("1.0.0");
        console.Out.Should().Contain("enabled");
        console.Out.Should().Contain("NoMercy");
    }
}
