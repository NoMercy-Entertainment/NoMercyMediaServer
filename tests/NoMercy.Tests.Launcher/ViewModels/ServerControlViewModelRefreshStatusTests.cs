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

using NoMercy.Launcher.Services;
using NoMercy.Launcher.ViewModels;
using NoMercy.Tests.Launcher.Support;
using Xunit;

namespace NoMercy.Tests.Launcher.ViewModels;

/// <summary>
/// REQUIREMENT: <c>RefreshStatusAsync</c>'s "server answered" branch is the
/// core status->display mapping the tray icon and the server-control tab both
/// depend on (status text, color, formatted uptime, "--" placeholders). This
/// exercises it through a real <see cref="ServerConnection"/> against a
/// <see cref="FakeManagementPipeServer"/>, complementing
/// <c>NoMercy.Tests.Service.ServerControlViewModelTests</c>'s existing
/// disconnected-branch and pure-function coverage.
/// </summary>
public sealed class ServerControlViewModelRefreshStatusTests
{
    private static Task<string> RespondWith(
        FakeManagementPipeServer server,
        int status,
        string reason,
        string body
    )
    {
        return server.RunOnceAsync(respond: stream =>
            FakeManagementPipeServer.WriteResponseAsync(stream: stream, statusCode: status, reasonPhrase: reason, body: body)
        );
    }

    [Fact]
    public async Task RefreshStatusAsync_RunningResponse_MapsStatusColorAndUptime()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: new ServerProcessLauncher());
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> statusRequest = RespondWith(
            server: server,
            status: 200,
            reason: "OK",
            body: """
                  {
                      "status": "running",
                      "server_name": "nomercy-test",
                      "version": "2.1.0",
                      "platform": "Windows",
                      "architecture": "X64",
                      "uptime_seconds": 3725,
                      "auto_start": true,
                      "update_available": true,
                      "restart_needed": false,
                      "latest_version": "2.2.0"
                  }
                  """
        );

        await viewModel.RefreshStatusAsync();

        await statusRequest;
        viewModel.ServerStatus.Should().Be(expected: "Running");
        viewModel.ServerName.Should().Be(expected: "nomercy-test");
        viewModel.Version.Should().Be(expected: "2.1.0");
        viewModel.Platform.Should().Be(expected: "Windows (X64)");
        viewModel.Uptime.Should().Be(expected: "1h 2m");
        viewModel.IsServerRunning.Should().BeTrue();
        viewModel.IsServerStopped.Should().BeFalse();
        viewModel.StatusColor.Should().Be(expected: "#22C55E");
        viewModel.AutoStartEnabled.Should().BeTrue();
        viewModel.UpdateAvailable.Should().BeTrue();
        viewModel.RestartNeeded.Should().BeFalse();
        viewModel.LatestVersion.Should().Be(expected: "2.2.0");
    }

    [Fact]
    public async Task RefreshStatusAsync_StartingResponse_MapsStartingColorAndLabel()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: new ServerProcessLauncher());
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> statusRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"starting"}""");

        await viewModel.RefreshStatusAsync();

        await statusRequest;
        viewModel.ServerStatus.Should().Be(expected: "Starting");
        viewModel.StatusColor.Should().Be(expected: "#EAB308");
        viewModel.IsServerRunning.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshStatusAsync_MissingServerNameAndVersion_FallsBackToPlaceholder()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: new ServerProcessLauncher());
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> statusRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");

        await viewModel.RefreshStatusAsync();

        await statusRequest;
        viewModel.ServerName.Should().Be(expected: "--");
        viewModel.Version.Should().Be(expected: "--");
        viewModel.Platform.Should().Be(expected: "--");
    }
}
