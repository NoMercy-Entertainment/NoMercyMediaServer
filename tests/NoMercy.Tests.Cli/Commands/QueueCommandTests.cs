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
/// REQUIREMENT: <c>queue status</c> must always show pending/failed counts when
/// reachable, and must only render the per-worker table when there is at least
/// one worker to show — an empty <c>Workers</c> dictionary must not print a
/// dangling header with no rows.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class QueueCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        RootCommand root = new(description: "test");
        root.Options.Add(item: pipeOption);
        root.Subcommands.Add(item: QueueCommand.Create(pipeOption: pipeOption, clientFactory: factory));
        return await root.Parse(args: ["queue", "status"]).InvokeAsync();
    }

    private static Mock<ICliClientFactory> FactoryReturning(QueueStatusResponse? status)
    {
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c =>
                c.GetAsync<QueueStatusResponse>(ApiRoutes.Queue, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: status);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);
        return factory;
    }

    [Fact]
    public async Task Status_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClientFactory> factory = FactoryReturning(status: null);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object);

        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Error.Should().Contain(expected: "Could not connect to server.");
    }

    [Fact]
    public async Task Status_NoWorkers_PrintsCountsOnly_WithoutWorkerTable()
    {
        QueueStatusResponse status = new()
        {
            PendingJobs = 4,
            FailedJobs = 1,
            Workers = new Dictionary<string, WorkerStatusResponse>(),
        };

        Mock<ICliClientFactory> factory = FactoryReturning(status: status);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object);

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        console.Out.Should().Contain(expected: "Pending Jobs:  4");
        console.Out.Should().Contain(expected: "Failed Jobs:   1");
        console.Out.Should().NotContain(unexpected: "Active Threads");
    }

    [Fact]
    public async Task Status_WithWorkers_PrintsWorkerTable()
    {
        QueueStatusResponse status = new()
        {
            PendingJobs = 2,
            FailedJobs = 0,
            Workers = new Dictionary<string, WorkerStatusResponse>
            {
                [key: "encoder"] = new() { ActiveThreads = 1 },
                [key: "library"] = new() { ActiveThreads = 0 },
            },
        };

        Mock<ICliClientFactory> factory = FactoryReturning(status: status);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory: factory.Object);

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        console.Out.Should().Contain(expected: "Active Threads");
        console.Out.Should().Contain(expected: "encoder");
        console.Out.Should().Contain(expected: "library");
    }
}
