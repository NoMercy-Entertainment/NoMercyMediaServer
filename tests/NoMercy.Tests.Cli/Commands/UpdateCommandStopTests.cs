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
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>update</c> must abort — never proceed to the wait-for-exit
/// / file-swap steps — if the stop request itself was not acknowledged. This
/// resolves without any timing dependency (unlike the wait-for-exit step),
/// since a failed POST is immediate.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UpdateCommandStopTests
{
    [Fact]
    public async Task Stop_NotAcknowledged_PrintsError_AndReturnsServerError_WithoutWaiting()
    {
        FakeManagementPipeServer server = new();
        Task<List<string>> requestsTask = server.RunSequenceAsync(
            stream =>
                FakeManagementPipeServer.WriteResponseAsync(
                    stream,
                    200,
                    "OK",
                    """{"status":"ok","message":"Downloaded"}"""
                ),
            stream =>
                FakeManagementPipeServer.WriteResponseAsync(
                    stream,
                    500,
                    "Internal Server Error",
                    ""
                )
        );

        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(UpdateCommand.Create(pipeOption, new CliClientFactory()));

        using ConsoleCapture console = new();
        int exitCode = await root.Parse(["--pipe", server.PipeName, "update"]).InvokeAsync();

        List<string> requests = await requestsTask;
        requests.Should().HaveCount(2);
        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Failed to send stop command.");
        console.Out.Should().NotContain("Waiting for server to exit");
    }
}
