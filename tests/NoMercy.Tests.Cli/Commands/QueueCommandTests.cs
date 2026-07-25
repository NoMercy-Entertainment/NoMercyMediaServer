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
[Trait("Category", "Unit")]
public sealed class QueueCommandTests
{
    private static async Task<int> RunAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(QueueCommand.Create(pipeOption, factory));
        return await root.Parse(["queue", "status"]).InvokeAsync();
    }

    private static Mock<ICliClientFactory> FactoryReturning(QueueStatusResponse? status)
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.GetAsync<QueueStatusResponse>(ApiRoutes.Queue, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(status);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);
        return factory;
    }

    [Fact]
    public async Task Status_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClientFactory> factory = FactoryReturning(null);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Could not connect to server.");
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

        Mock<ICliClientFactory> factory = FactoryReturning(status);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Pending Jobs:  4");
        console.Out.Should().Contain("Failed Jobs:   1");
        console.Out.Should().NotContain("Active Threads");
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
                ["encoder"] = new() { ActiveThreads = 1 },
                ["library"] = new() { ActiveThreads = 0 },
            },
        };

        Mock<ICliClientFactory> factory = FactoryReturning(status);

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Active Threads");
        console.Out.Should().Contain("encoder");
        console.Out.Should().Contain("library");
    }
}
