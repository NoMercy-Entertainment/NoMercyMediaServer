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
using Xunit;

namespace NoMercy.Tests.Service;

public class ServerControlViewModelTests
{
    [Theory]
    [InlineData(data: ["running", "Running"])]
    [InlineData(data: ["starting", "Starting"])]
    [InlineData(data: ["Disconnected", "Disconnected"])]
    [InlineData(data: ["unknown", "unknown"])]
    public void FormatStatusDisplay_ReturnsExpectedLabel(string input, string expected)
    {
        string result = ServerControlViewModel.FormatStatusDisplay(status: input);
        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(data: ["running", "#22C55E"])]
    [InlineData(data: ["Running", "#22C55E"])]
    [InlineData(data: ["starting", "#EAB308"])]
    [InlineData(data: ["Starting", "#EAB308"])]
    [InlineData(data: ["Disconnected", "#EF4444"])]
    [InlineData(data: ["unknown", "#EF4444"])]
    public void GetStatusColor_ReturnsExpectedColor(string input, string expected)
    {
        string result = ServerControlViewModel.GetStatusColor(status: input);
        Assert.Equal(expected: expected, actual: result);
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        ServerConnection connection = new();

        ServerProcessLauncher launcher = new();
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: launcher);

        Assert.Equal(expected: "Disconnected", actual: viewModel.ServerStatus);
        Assert.Equal(expected: "--", actual: viewModel.ServerName);
        Assert.Equal(expected: "--", actual: viewModel.Version);
        Assert.Equal(expected: "--", actual: viewModel.Platform);
        Assert.Equal(expected: "--", actual: viewModel.Uptime);
        Assert.False(condition: viewModel.IsServerRunning);
        Assert.False(condition: viewModel.IsActionInProgress);
        Assert.Equal(expected: string.Empty, actual: viewModel.ActionStatus);
        Assert.Equal(expected: "#EF4444", actual: viewModel.StatusColor);

        connection.Dispose();
    }

    [Fact]
    public void PropertyChanged_FiresOnStatusChange()
    {
        ServerConnection connection = new();
        ServerProcessLauncher launcher = new();
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: launcher);
        List<string> changedProperties = [];

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(item: e.PropertyName);
        };

        viewModel.ServerStatus = "Running";
        viewModel.ServerName = "TestServer";
        viewModel.IsServerRunning = true;
        viewModel.ActionStatus = "Working...";

        Assert.Contains(expected: "ServerStatus", collection: changedProperties);
        Assert.Contains(expected: "ServerName", collection: changedProperties);
        Assert.Contains(expected: "IsServerRunning", collection: changedProperties);
        Assert.Contains(expected: "ActionStatus", collection: changedProperties);

        connection.Dispose();
    }

    [Fact]
    public void PropertyChanged_FiresForAllProperties()
    {
        ServerConnection connection = new();
        ServerProcessLauncher launcher = new();
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: launcher);
        List<string> changedProperties = [];

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(item: e.PropertyName);
        };

        viewModel.Version = "2.0.0";
        viewModel.Platform = "Linux (X64)";
        viewModel.Uptime = "1h 30m";
        viewModel.IsActionInProgress = true;
        viewModel.StatusColor = "#22C55E";

        Assert.Contains(expected: "Version", collection: changedProperties);
        Assert.Contains(expected: "Platform", collection: changedProperties);
        Assert.Contains(expected: "Uptime", collection: changedProperties);
        Assert.Contains(expected: "IsActionInProgress", collection: changedProperties);
        Assert.Contains(expected: "StatusColor", collection: changedProperties);

        connection.Dispose();
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenDisconnected_SetsDefaults()
    {
        // A unique pipe name no server can be listening on — the machine-global
        // default pipe would answer when a live dev server runs on this box.
        ServerConnection connection = new(pipeNameOrSocketPath: $"nomercy-test-{Guid.NewGuid():N}");
        ServerProcessLauncher launcher = new();
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: launcher);

        await viewModel.RefreshStatusAsync();

        Assert.Equal(expected: "Disconnected", actual: viewModel.ServerStatus);
        Assert.Equal(expected: "--", actual: viewModel.ServerName);
        Assert.Equal(expected: "--", actual: viewModel.Version);
        Assert.Equal(expected: "--", actual: viewModel.Platform);
        Assert.Equal(expected: "--", actual: viewModel.Uptime);
        Assert.False(condition: viewModel.IsServerRunning);
        Assert.Equal(expected: "#EF4444", actual: viewModel.StatusColor);

        connection.Dispose();
    }

    [Fact]
    public void StartPolling_ThenStopPolling_DoesNotThrow()
    {
        ServerConnection connection = new();
        ServerProcessLauncher launcher = new();
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: launcher);

        viewModel.StartPolling();
        viewModel.StopPolling();

        connection.Dispose();
    }

    [Fact]
    public void StopPolling_WithoutStarting_DoesNotThrow()
    {
        ServerConnection connection = new();
        ServerProcessLauncher launcher = new();
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: launcher);

        viewModel.StopPolling();

        connection.Dispose();
    }

    [Fact]
    public void StartPolling_CalledTwice_DoesNotThrow()
    {
        ServerConnection connection = new();
        ServerProcessLauncher launcher = new();
        ServerControlViewModel viewModel = new(serverConnection: connection, processLauncher: launcher);

        viewModel.StartPolling();
        viewModel.StartPolling();
        viewModel.StopPolling();

        connection.Dispose();
    }
}
