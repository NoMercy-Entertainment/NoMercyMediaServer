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
/// <see cref="SettingsViewModel"/>'s Load/Save round-trip through a real
/// <see cref="ServerConnection"/> against a <see cref="FakeManagementPipeServer"/>
/// — the real named-pipe + JSON wire format, not a mocked connection.
/// </summary>
public sealed class SettingsViewModelTests
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
    public async Task LoadConfigAsync_SuccessResponse_MapsEveryFieldAndMarksLoaded()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        SettingsViewModel viewModel = new(serverConnection: connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> configRequest = RespondWith(
            server: server,
            status: 200,
            reason: "OK",
            body: """
                  {
                      "internal_port": 7626,
                      "external_port": 7627,
                      "server_name": "nomercy-test",
                      "library_workers": 1,
                      "import_workers": 2,
                      "extras_workers": 15,
                      "encoder_workers": 1,
                      "cron_workers": 1,
                      "image_workers": 10,
                      "file_workers": 4,
                      "music_workers": 2,
                      "swagger": false
                  }
                  """
        );

        await viewModel.LoadConfigAsync();

        await configRequest;
        viewModel.ConfigLoaded.Should().BeTrue();
        viewModel.ConfigServerName.Should().Be(expected: "nomercy-test");
        viewModel.InternalPort.Should().Be(expected: 7626);
        viewModel.ExternalPort.Should().Be(expected: 7627);
        viewModel.LibraryWorkers.Should().Be(expected: 1);
        viewModel.ImportWorkers.Should().Be(expected: 2);
        viewModel.ExtrasWorkers.Should().Be(expected: 15);
        viewModel.EncoderWorkers.Should().Be(expected: 1);
        viewModel.CronWorkers.Should().Be(expected: 1);
        viewModel.ImageWorkers.Should().Be(expected: 10);
        viewModel.FileWorkers.Should().Be(expected: 4);
        viewModel.MusicWorkers.Should().Be(expected: 2);
    }

    [Fact]
    public async Task LoadConfigAsync_NullServerName_FallsBackToEmptyString()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        SettingsViewModel viewModel = new(serverConnection: connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> configRequest = RespondWith(
            server: server,
            status: 200,
            reason: "OK",
            body: """{"internal_port":7626,"server_name":null}"""
        );

        await viewModel.LoadConfigAsync();

        await configRequest;
        viewModel.ConfigServerName.Should().Be(expected: string.Empty);
        viewModel.ConfigLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task LoadConfigAsync_ErrorResponse_LeavesConfigLoadedFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        SettingsViewModel viewModel = new(serverConnection: connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> configRequest = RespondWith(server: server, status: 404, reason: "Not Found", body: "");

        await viewModel.LoadConfigAsync();

        await configRequest;
        viewModel.ConfigLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task SaveConfigAsync_SuccessResponse_SetsSavedStatus()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        SettingsViewModel viewModel = new(serverConnection: connection) { ConfigServerName = "renamed" };
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> saveRequest = RespondWith(server: server, status: 200, reason: "OK", body: "");

        await viewModel.SaveConfigAsync();

        string request = await saveRequest;
        request.Should().StartWith(expected: "PUT /manage/config");
        request.Should().Contain(expected: "\"server_name\":\"renamed\"");
        viewModel.ActionStatus.Should().Be(expected: "Configuration saved");
    }

    [Fact]
    public async Task SaveConfigAsync_ErrorResponse_SetsFailureStatus()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        SettingsViewModel viewModel = new(serverConnection: connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> saveRequest = RespondWith(server: server, status: 500, reason: "Internal Server Error", body: "");

        await viewModel.SaveConfigAsync();

        await saveRequest;
        viewModel.ActionStatus.Should().Be(expected: "Failed to save configuration");
    }

    [Fact]
    public async Task SaveConfigAsync_NoServerReachable_SetsFailureStatusInsteadOfThrowing()
    {
        using ServerConnection connection = new(pipeNameOrSocketPath: $"nomercy-test-{Guid.NewGuid():N}");
        SettingsViewModel viewModel = new(serverConnection: connection);

        await viewModel.SaveConfigAsync();

        viewModel.ActionStatus.Should().Be(expected: "Failed to save configuration");
    }

    [Fact]
    public void PropertyChanged_FiresForWorkerCountProperties()
    {
        SettingsViewModel viewModel = new(serverConnection: new ServerConnection());
        List<string> changed = [];
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changed.Add(item: e.PropertyName);
        };

        viewModel.LibraryWorkers = 4;
        viewModel.EncoderWorkers = 2;

        changed.Should().Contain(expected: nameof(SettingsViewModel.LibraryWorkers));
        changed.Should().Contain(expected: nameof(SettingsViewModel.EncoderWorkers));
    }
}
