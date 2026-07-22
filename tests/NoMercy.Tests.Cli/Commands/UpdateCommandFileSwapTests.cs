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
using NoMercy.NmSystem.Information;
using NoMercy.Tests.Cli.Support;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>update</c> only ever applies the staged binary once the
/// download+stop+wait steps have all run — a missing staged file must fail
/// loudly instead of silently doing nothing, an existing current executable
/// must be deleted before the move (a bare <c>File.Move</c> onto an existing
/// path throws), and the staged file must be MOVED (not copied) so a stale
/// temp file can never be mistaken for "still staged" on the next update.
///
/// <see cref="AppFiles.ServerExePath"/>/<see cref="AppFiles.ServerTempExePath"/>
/// are namespaced under the test-isolated NOMERCY_APP_PATH root, so this drives
/// the real file-system swap rather than faking it — only the download/stop/
/// wait-for-exit network steps ahead of it go through a
/// <see cref="FakeManagementPipeServer"/>.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class UpdateCommandFileSwapTests : IDisposable
{
    private readonly string _tempExePath = AppFiles.ServerTempExePath;
    private readonly string _currentExePath = AppFiles.ServerExePath;

    public UpdateCommandFileSwapTests()
    {
        Directory.CreateDirectory(path: AppFiles.BinariesPath);
        DeleteIfExists(path: _tempExePath);
        DeleteIfExists(path: _currentExePath);
    }

    public void Dispose()
    {
        DeleteIfExists(path: _tempExePath);
        DeleteIfExists(path: _currentExePath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path: path))
            File.Delete(path: path);
    }

    /// <summary>
    /// Runs the full <c>update</c> action against a fake pipe server that
    /// answers "ok" to the download and "true" to the stop request, then
    /// drops the third (status-poll) connection immediately so
    /// <c>WaitForServerExitAsync</c> resolves as "confirmed stopped" on its
    /// first check instead of waiting out the real 30s timeout.
    /// </summary>
    private static async Task<int> RunPastWaitForExitAsync()
    {
        FakeManagementPipeServer server = new();
        Task<List<string>> requestsTask = server.RunSequenceAsync(responders:
            [
                stream =>
                    FakeManagementPipeServer.WriteResponseAsync(
                        stream: stream,
                        statusCode: 200,
                        reasonPhrase: "OK",
                        body: """{"status":"ok","message":"Downloaded"}"""
                    ),
                stream => FakeManagementPipeServer.WriteResponseAsync(stream: stream, statusCode: 200, reasonPhrase: "OK", body: "true"), _ => Task.CompletedTask
            ]
        );

        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        RootCommand root = new(description: "test");
        root.Options.Add(item: pipeOption);
        root.Subcommands.Add(item: UpdateCommand.Create(pipeOption: pipeOption, clientFactory: new CliClientFactory()));

        int exitCode = await root.Parse(args: ["--pipe", server.PipeName, "update"]).InvokeAsync();
        await requestsTask;
        return exitCode;
    }

    [Fact]
    public async Task NoStagedUpdateFile_PrintsError_AndReturnsServerError()
    {
        using ConsoleCapture console = new();
        int exitCode = await RunPastWaitForExitAsync();

        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Error.Should().Contain(expected: "No staged update file found.");
    }

    [Fact]
    public async Task StagedFileExists_ExistingCurrentExecutable_IsReplaced()
    {
        File.WriteAllText(path: _currentExePath, contents: "OLD");
        File.WriteAllText(path: _tempExePath, contents: "NEW");

        using ConsoleCapture console = new();
        int exitCode = await RunPastWaitForExitAsync();

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        console.Out.Should().Contain(expected: "Update applied.");
        File.Exists(path: _tempExePath).Should().BeFalse();
        File.Exists(path: _currentExePath).Should().BeTrue();
        File.ReadAllText(path: _currentExePath).Should().Be(expected: "NEW");
    }

    [Fact]
    public async Task StagedFileExists_NoExistingCurrentExecutable_IsMovedIntoPlace()
    {
        File.WriteAllText(path: _tempExePath, contents: "NEW");

        using ConsoleCapture console = new();
        int exitCode = await RunPastWaitForExitAsync();

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        File.Exists(path: _tempExePath).Should().BeFalse();
        File.ReadAllText(path: _currentExePath).Should().Be(expected: "NEW");
    }
}
