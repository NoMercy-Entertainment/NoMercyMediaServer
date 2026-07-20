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
        return server.RunOnceAsync(stream =>
            FakeManagementPipeServer.WriteResponseAsync(stream, status, reason, body)
        );
    }

    [Fact]
    public async Task LoadConfigAsync_SuccessResponse_MapsEveryFieldAndMarksLoaded()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        SettingsViewModel viewModel = new(connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> configRequest = RespondWith(
            server,
            200,
            "OK",
            """
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
        viewModel.ConfigServerName.Should().Be("nomercy-test");
        viewModel.InternalPort.Should().Be(7626);
        viewModel.ExternalPort.Should().Be(7627);
        viewModel.LibraryWorkers.Should().Be(1);
        viewModel.ImportWorkers.Should().Be(2);
        viewModel.ExtrasWorkers.Should().Be(15);
        viewModel.EncoderWorkers.Should().Be(1);
        viewModel.CronWorkers.Should().Be(1);
        viewModel.ImageWorkers.Should().Be(10);
        viewModel.FileWorkers.Should().Be(4);
        viewModel.MusicWorkers.Should().Be(2);
    }

    [Fact]
    public async Task LoadConfigAsync_NullServerName_FallsBackToEmptyString()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        SettingsViewModel viewModel = new(connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> configRequest = RespondWith(
            server,
            200,
            "OK",
            """{"internal_port":7626,"server_name":null}"""
        );

        await viewModel.LoadConfigAsync();

        await configRequest;
        viewModel.ConfigServerName.Should().Be(string.Empty);
        viewModel.ConfigLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task LoadConfigAsync_ErrorResponse_LeavesConfigLoadedFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        SettingsViewModel viewModel = new(connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> configRequest = RespondWith(server, 404, "Not Found", "");

        await viewModel.LoadConfigAsync();

        await configRequest;
        viewModel.ConfigLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task SaveConfigAsync_SuccessResponse_SetsSavedStatus()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        SettingsViewModel viewModel = new(connection) { ConfigServerName = "renamed" };
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> saveRequest = RespondWith(server, 200, "OK", "");

        await viewModel.SaveConfigAsync();

        string request = await saveRequest;
        request.Should().StartWith("PUT /manage/config");
        request.Should().Contain("\"server_name\":\"renamed\"");
        viewModel.ActionStatus.Should().Be("Configuration saved");
    }

    [Fact]
    public async Task SaveConfigAsync_ErrorResponse_SetsFailureStatus()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        SettingsViewModel viewModel = new(connection);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> saveRequest = RespondWith(server, 500, "Internal Server Error", "");

        await viewModel.SaveConfigAsync();

        await saveRequest;
        viewModel.ActionStatus.Should().Be("Failed to save configuration");
    }

    [Fact]
    public async Task SaveConfigAsync_NoServerReachable_SetsFailureStatusInsteadOfThrowing()
    {
        using ServerConnection connection = new($"nomercy-test-{Guid.NewGuid():N}");
        SettingsViewModel viewModel = new(connection);

        await viewModel.SaveConfigAsync();

        viewModel.ActionStatus.Should().Be("Failed to save configuration");
    }

    [Fact]
    public void PropertyChanged_FiresForWorkerCountProperties()
    {
        SettingsViewModel viewModel = new(new ServerConnection());
        List<string> changed = [];
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changed.Add(e.PropertyName);
        };

        viewModel.LibraryWorkers = 4;
        viewModel.EncoderWorkers = 2;

        changed.Should().Contain(nameof(SettingsViewModel.LibraryWorkers));
        changed.Should().Contain(nameof(SettingsViewModel.EncoderWorkers));
    }
}
