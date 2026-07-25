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
/// REQUIREMENT: <c>resources</c> always shows CPU/memory, and independently
/// shows the GPU and storage sections only when the server actually reports at
/// least one GPU / one drive — a server with no GPU must not print an empty
/// "GPU 0:" line, and the per-drive "used" figure must be computed as
/// total-minus-available, not echoed from the server.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResourcesCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(ResourcesCommand.Create(pipeOption, factory));
        return await root.Parse(["resources"]).InvokeAsync();
    }

    private static Mock<ICliClientFactory> FactoryReturning(ResourcesResponse? resources)
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.GetAsync<ResourcesResponse>(ApiRoutes.Resources, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(resources);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);
        return factory;
    }

    [Fact]
    public async Task Resources_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClientFactory> factory = FactoryReturning(null);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Could not retrieve resource information.");
    }

    [Fact]
    public async Task Resources_NoGpuOrStorage_PrintsCpuAndMemoryOnly()
    {
        ResourcesResponse resources = new()
        {
            Cpu = new() { Total = 12.5, Max = 100 },
            Memory = new()
            {
                Use = 4.2,
                Total = 16,
                Percentage = 26.25,
            },
            Gpu = [],
            Storage = [],
        };

        Mock<ICliClientFactory> factory = FactoryReturning(resources);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("CPU:          12.5% (max 100.0%)");
        console.Out.Should().Contain("Memory:       4.2 / 16.0 GB (26.2%)");
        console.Out.Should().NotContain("GPU");
        console.Out.Should().NotContain("Storage:");
    }

    [Fact]
    public async Task Resources_WithGpuAndStorage_PrintsBothSections_AndComputesUsedSpace()
    {
        ResourcesResponse resources = new()
        {
            Cpu = new() { Total = 5, Max = 100 },
            Memory = new()
            {
                Use = 1,
                Total = 8,
                Percentage = 12.5,
            },
            Gpu =
            [
                new()
                {
                    Index = 0,
                    Core = 10,
                    Memory = 20,
                    Encode = 30,
                    Decode = 40,
                },
            ],
            Storage =
            [
                new()
                {
                    Name = "C:",
                    Total = 500,
                    Available = 200,
                    Percentage = 40,
                },
            ],
        };

        Mock<ICliClientFactory> factory = FactoryReturning(resources);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console
            .Out.Should()
            .Contain("GPU 0:        10.0% core, 20.0% memory, 30.0% encode, 40.0% decode");
        console.Out.Should().Contain("Storage:");
        console.Out.Should().Contain("300.0 / 500.0 GB (40.0% free)");
    }
}
