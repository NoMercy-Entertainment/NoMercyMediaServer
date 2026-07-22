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
/// REQUIREMENT: <c>update</c> must stop at the download step — never proceed
/// to stop the running server — unless the server explicitly confirms the
/// download with <c>{"status":"ok"}</c>. A missing/unreachable response and a
/// non-"ok" status must both fail the command, and the message shown must
/// fall back to a fixed string only when the server didn't supply one. The
/// response DTO is a private nested type, so this runs against a real
/// <see cref="FakeManagementPipeServer"/> rather than a mock.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class UpdateCommandDownloadTests
{
    private static async Task<int> RunAsync(string pipeName)
    {
        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        RootCommand root = new(description: "test");
        root.Options.Add(item: pipeOption);
        root.Subcommands.Add(item: UpdateCommand.Create(pipeOption: pipeOption, clientFactory: new CliClientFactory()));
        return await root.Parse(args: ["--pipe", pipeName, "update"]).InvokeAsync();
    }

    [Fact]
    public async Task Download_ServerUnreachable_PrintsFallbackMessage_AndReturnsServerError()
    {
        FakeManagementPipeServer server = new();
        Task<string> serverTask = server.RunOnceAsync(respond: stream =>
            FakeManagementPipeServer.WriteResponseAsync(stream: stream, statusCode: 500, reasonPhrase: "Internal Server Error", body: "")
        );

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(pipeName: server.PipeName);

        await serverTask;
        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Error.Should().Contain(expected: "Failed to download update.");
    }

    [Fact]
    public async Task Download_StatusNotOk_PrintsServerMessage_AndReturnsServerError_WithoutStopping()
    {
        FakeManagementPipeServer server = new();
        Task<string> serverTask = server.RunOnceAsync(respond: stream =>
            FakeManagementPipeServer.WriteResponseAsync(
                stream: stream,
                statusCode: 200,
                reasonPhrase: "OK",
                body: """{"status":"fail","message":"disk full"}"""
            )
        );

        using ConsoleCapture console = new();
        int exitCode = await RunAsync(pipeName: server.PipeName);

        string request = await serverTask;
        request.Should().StartWith(expected: "POST /manage/update");
        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Error.Should().Contain(expected: "disk full");
    }

    [Fact]
    public async Task Download_Ok_PrintsMessage_AndProceedsToStop()
    {
        FakeManagementPipeServer server = new();
        Task<List<string>> requestsTask = server.RunSequenceAsync(responders:
            [
                stream =>
                    FakeManagementPipeServer.WriteResponseAsync(
                        stream: stream,
                        statusCode: 200,
                        reasonPhrase: "OK",
                        body: """{"status":"ok","message":"Downloaded 120MB"}"""
                    ),
                stream => FakeManagementPipeServer.WriteResponseAsync(stream: stream, statusCode: 200, reasonPhrase: "OK", body: "true"), // The run continues past stop into the wait-for-exit poll once the
                // two responders above are exhausted. Accepting that third
                // connection and dropping it immediately (no response written)
                // makes the client observe a fast connection failure instead of
                // burning the real ~3s named-pipe connect timeout on a pipe name
                // nothing is listening on.
                _ => Task.CompletedTask
            ]
        );

        using ConsoleCapture console = new();
        // This test only asserts the download step's own console output and
        // that a second request (stop) really was sent — it deliberately
        // ignores the overall exit code, since the run continues past stop
        // into the wait-for-exit/file-swap steps (covered separately).
        _ = await RunAsync(pipeName: server.PipeName);

        List<string> requests = await requestsTask;
        requests.Should().HaveCount(expected: 3);
        requests[index: 0].Should().StartWith(expected: "POST /manage/update");
        requests[index: 1].Should().StartWith(expected: "POST /manage/stop");
        console.Out.Should().Contain(expected: "Downloading update...");
        console.Out.Should().Contain(expected: "Downloaded 120MB");
        console.Out.Should().Contain(expected: "Stopping server...");
    }
}
