using Newtonsoft.Json;
using NoMercy.Tray.Models;
using Xunit;

namespace NoMercy.Tests.Tray;

public class ServerStatusResponseTests
{
    [Fact]
    public void Deserialize_FullResponse_MapsAllProperties()
    {
        string json = """
        {
            "status": "running",
            "server_name": "TestServer",
            "version": "1.2.3",
            "platform": "Linux",
            "architecture": "X64",
            "os": "Linux 6.1",
            "uptime_seconds": 3600,
            "start_time": "2026-01-01T00:00:00Z",
            "is_dev": true
        }
        """;

        ServerStatusResponse? result =
            JsonConvert.DeserializeObject<ServerStatusResponse>(json);

        Assert.NotNull(result);
        Assert.Equal("running", result.Status);
        Assert.Equal("TestServer", result.ServerName);
        Assert.Equal("1.2.3", result.Version);
        Assert.Equal("Linux", result.Platform);
        Assert.Equal("X64", result.Architecture);
        Assert.Equal("Linux 6.1", result.Os);
        Assert.Equal(3600, result.UptimeSeconds);
        Assert.True(result.IsDev);
    }

    [Fact]
    public void Deserialize_MinimalResponse_UsesDefaults()
    {
        string json = """{ "status": "running" }""";

        ServerStatusResponse? result =
            JsonConvert.DeserializeObject<ServerStatusResponse>(json);

        Assert.NotNull(result);
        Assert.Equal("running", result.Status);
        Assert.Equal(string.Empty, result.ServerName);
        Assert.Equal(string.Empty, result.Version);
        Assert.Equal(string.Empty, result.Platform);
        Assert.Equal(string.Empty, result.Architecture);
        Assert.Equal(string.Empty, result.Os);
        Assert.Equal(0, result.UptimeSeconds);
        Assert.False(result.IsDev);
    }

    [Fact]
    public void Deserialize_StartingStatus_MapsCorrectly()
    {
        string json = """{ "status": "starting", "version": "0.9.0" }""";

        ServerStatusResponse? result =
            JsonConvert.DeserializeObject<ServerStatusResponse>(json);

        Assert.NotNull(result);
        Assert.Equal("starting", result.Status);
        Assert.Equal("0.9.0", result.Version);
    }
}
