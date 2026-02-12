using NoMercy.Tray.Services;
using NoMercy.Tray.ViewModels;
using Xunit;

namespace NoMercy.Tests.Tray;

public class ServerControlViewModelTests
{
    [Theory]
    [InlineData("running", "Running")]
    [InlineData("starting", "Starting")]
    [InlineData("Disconnected", "Disconnected")]
    [InlineData("unknown", "unknown")]
    public void FormatStatusDisplay_ReturnsExpectedLabel(
        string input, string expected)
    {
        string result = ServerControlViewModel.FormatStatusDisplay(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("running", "#22C55E")]
    [InlineData("Running", "#22C55E")]
    [InlineData("starting", "#EAB308")]
    [InlineData("Starting", "#EAB308")]
    [InlineData("Disconnected", "#EF4444")]
    [InlineData("unknown", "#EF4444")]
    public void GetStatusColor_ReturnsExpectedColor(
        string input, string expected)
    {
        string result = ServerControlViewModel.GetStatusColor(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        ServerConnection connection = new();

        ServerControlViewModel viewModel = new(connection);

        Assert.Equal("Disconnected", viewModel.ServerStatus);
        Assert.Equal("--", viewModel.ServerName);
        Assert.Equal("--", viewModel.Version);
        Assert.Equal("--", viewModel.Platform);
        Assert.Equal("--", viewModel.Uptime);
        Assert.False(viewModel.IsServerRunning);
        Assert.False(viewModel.IsActionInProgress);
        Assert.Equal(string.Empty, viewModel.ActionStatus);
        Assert.Equal("#EF4444", viewModel.StatusColor);

        connection.Dispose();
    }

    [Fact]
    public void PropertyChanged_FiresOnStatusChange()
    {
        ServerConnection connection = new();
        ServerControlViewModel viewModel = new(connection);
        List<string> changedProperties = [];

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(e.PropertyName);
        };

        viewModel.ServerStatus = "Running";
        viewModel.ServerName = "TestServer";
        viewModel.IsServerRunning = true;
        viewModel.ActionStatus = "Working...";

        Assert.Contains("ServerStatus", changedProperties);
        Assert.Contains("ServerName", changedProperties);
        Assert.Contains("IsServerRunning", changedProperties);
        Assert.Contains("ActionStatus", changedProperties);

        connection.Dispose();
    }

    [Fact]
    public void PropertyChanged_FiresForAllProperties()
    {
        ServerConnection connection = new();
        ServerControlViewModel viewModel = new(connection);
        List<string> changedProperties = [];

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(e.PropertyName);
        };

        viewModel.Version = "2.0.0";
        viewModel.Platform = "Linux (X64)";
        viewModel.Uptime = "1h 30m";
        viewModel.IsActionInProgress = true;
        viewModel.StatusColor = "#22C55E";

        Assert.Contains("Version", changedProperties);
        Assert.Contains("Platform", changedProperties);
        Assert.Contains("Uptime", changedProperties);
        Assert.Contains("IsActionInProgress", changedProperties);
        Assert.Contains("StatusColor", changedProperties);

        connection.Dispose();
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenDisconnected_SetsDefaults()
    {
        ServerConnection connection = new();
        ServerControlViewModel viewModel = new(connection);

        await viewModel.RefreshStatusAsync();

        Assert.Equal("Disconnected", viewModel.ServerStatus);
        Assert.Equal("--", viewModel.ServerName);
        Assert.Equal("--", viewModel.Version);
        Assert.Equal("--", viewModel.Platform);
        Assert.Equal("--", viewModel.Uptime);
        Assert.False(viewModel.IsServerRunning);
        Assert.Equal("#EF4444", viewModel.StatusColor);

        connection.Dispose();
    }

    [Fact]
    public void StartPolling_ThenStopPolling_DoesNotThrow()
    {
        ServerConnection connection = new();
        ServerControlViewModel viewModel = new(connection);

        viewModel.StartPolling();
        viewModel.StopPolling();

        connection.Dispose();
    }

    [Fact]
    public void StopPolling_WithoutStarting_DoesNotThrow()
    {
        ServerConnection connection = new();
        ServerControlViewModel viewModel = new(connection);

        viewModel.StopPolling();

        connection.Dispose();
    }

    [Fact]
    public void StartPolling_CalledTwice_DoesNotThrow()
    {
        ServerConnection connection = new();
        ServerControlViewModel viewModel = new(connection);

        viewModel.StartPolling();
        viewModel.StartPolling();
        viewModel.StopPolling();

        connection.Dispose();
    }
}
