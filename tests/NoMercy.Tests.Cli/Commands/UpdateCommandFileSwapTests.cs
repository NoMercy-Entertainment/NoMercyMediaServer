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
using NoMercy.Tests.Common.Ipc;
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
[Trait("Category", "Unit")]
public sealed class UpdateCommandFileSwapTests : IDisposable
{
    private readonly string _tempExePath = AppFiles.ServerTempExePath;
    private readonly string _currentExePath = AppFiles.ServerExePath;

    public UpdateCommandFileSwapTests()
    {
        Directory.CreateDirectory(AppFiles.BinariesPath);
        DeleteIfExists(_tempExePath);
        DeleteIfExists(_currentExePath);
    }

    public void Dispose()
    {
        DeleteIfExists(_tempExePath);
        DeleteIfExists(_currentExePath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
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
        Task<List<string>> requestsTask = server.RunSequenceAsync([
            stream =>
                FakeManagementPipeServer.WriteResponseAsync(
                    stream,
                    200,
                    "OK",
                    """{"status":"ok","message":"Downloaded"}"""
                ),
            stream => FakeManagementPipeServer.WriteResponseAsync(stream, 200, "OK", "true"),
            _ => Task.CompletedTask,
        ]);

        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(
            UpdateCommand.Create(
                pipeOption,
                new CliClientFactory(),
                startServer: _ => true,
                awaitVersion: (_, _) => Task.FromResult<string?>("9.9.9")
            )
        );

        int exitCode = await root.Parse(["--pipe", server.PipeName, "update"]).InvokeAsync();
        await requestsTask;
        return exitCode;
    }

    [Fact]
    public async Task NoStagedUpdateFile_PrintsError_AndLeavesTheServerRunning()
    {
        // Only the download is answered. With nothing staged the run must stop right there:
        // taking a healthy server down for an update that was never staged is a self-inflicted
        // outage, so the stop request is never sent.
        FakeManagementPipeServer server = new();
        Task<List<string>> requestsTask = server.RunSequenceAsync([
            stream =>
                FakeManagementPipeServer.WriteResponseAsync(
                    stream,
                    200,
                    "OK",
                    """{"status":"ok","message":"Downloaded"}"""
                ),
        ]);

        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(
            UpdateCommand.Create(
                pipeOption,
                new CliClientFactory(),
                startServer: _ => true,
                awaitVersion: (_, _) => Task.FromResult<string?>("9.9.9")
            )
        );

        using ConsoleCapture console = new();
        int exitCode = await root.Parse(["--pipe", server.PipeName, "update"]).InvokeAsync();
        List<string> requests = await requestsTask;

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("No staged update file found");
        requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task StagedFileExists_ExistingCurrentExecutable_IsReplaced()
    {
        File.WriteAllText(_currentExePath, "OLD");
        File.WriteAllText(_tempExePath, "NEW");

        using ConsoleCapture console = new();
        int exitCode = await RunPastWaitForExitAsync();

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Update applied.");
        File.Exists(_tempExePath).Should().BeFalse();
        File.Exists(_currentExePath).Should().BeTrue();
        File.ReadAllText(_currentExePath).Should().Be("NEW");
    }

    [Fact]
    public async Task StagedFileExists_NoExistingCurrentExecutable_IsMovedIntoPlace()
    {
        File.WriteAllText(_tempExePath, "NEW");

        using ConsoleCapture console = new();
        int exitCode = await RunPastWaitForExitAsync();

        exitCode.Should().Be((int)ExitCode.Success);
        File.Exists(_tempExePath).Should().BeFalse();
        File.ReadAllText(_currentExePath).Should().Be("NEW");
    }
}
