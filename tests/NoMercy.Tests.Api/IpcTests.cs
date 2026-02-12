using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using NoMercy.Networking;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class IpcClientTests
{
    [Fact]
    public void IpcClient_CanBeCreated_WithDefaults()
    {
        using IpcClient client = new();

        Assert.NotNull(client);
    }

    [Fact]
    public void IpcClient_CanBeCreated_WithCustomPath()
    {
        using IpcClient client = new("/tmp/test-nomercy.sock");

        Assert.NotNull(client);
    }

    [Fact]
    public void IpcClient_CanBeDisposed_MultipleTimes()
    {
        IpcClient client = new();
        client.Dispose();
        client.Dispose(); // Should not throw
    }

    [Fact]
    public void Config_ManagementPipeName_HasDefault()
    {
        Assert.Equal("NoMercyManagement", Config.ManagementPipeName);
    }

    [Fact]
    public void Config_ManagementPipeName_CanBeSet()
    {
        string original = Config.ManagementPipeName;
        try
        {
            Config.ManagementPipeName = "TestPipe";
            Assert.Equal("TestPipe", Config.ManagementPipeName);
        }
        finally
        {
            Config.ManagementPipeName = original;
        }
    }

    [Fact]
    public void Config_ManagementSocketPath_IsUnderAppPath()
    {
        string socketPath = Config.ManagementSocketPath;

        Assert.StartsWith(AppFiles.AppPath, socketPath);
        Assert.EndsWith(".sock", socketPath);
    }
}

[Trait("Category", "Integration")]
public class IpcUnixSocketIntegrationTests : IDisposable
{
    private readonly string _socketPath;
    private readonly Socket _listenSocket;

    public IpcUnixSocketIntegrationTests()
    {
        _socketPath = Path.Combine(Path.GetTempPath(), $"nomercy-test-{Guid.NewGuid():N}.sock");
        _listenSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listenSocket.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listenSocket.Listen(1);
    }

    [Fact]
    public async Task IpcClient_ConnectsToUnixSocket_AndSendsRequest()
    {
        if (OperatingSystem.IsWindows())
            return; // Unix sockets only on Linux/macOS

        // Arrange — fake HTTP server on the socket
        Task<string> serverTask = Task.Run(async () =>
        {
            using Socket accepted = await _listenSocket.AcceptAsync();
            using NetworkStream stream = new(accepted);

            byte[] buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            string responseBody = JsonSerializer.Serialize(new { status = "running" });
            string httpResponse = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n{responseBody}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(httpResponse);
            await stream.WriteAsync(responseBytes);

            return request;
        });

        // Act
        using IpcClient client = new(_socketPath);
        HttpResponseMessage response = await client.GetAsync("/manage/status");

        // Assert
        string receivedRequest = await serverTask;
        Assert.Contains("GET /manage/status", receivedRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);
        Assert.Equal("running", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task IpcClient_CanPostToUnixSocket()
    {
        if (OperatingSystem.IsWindows())
            return;

        Task serverTask = Task.Run(async () =>
        {
            using Socket accepted = await _listenSocket.AcceptAsync();
            using NetworkStream stream = new(accepted);

            byte[] buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer);

            string responseBody = JsonSerializer.Serialize(new { status = "ok", message = "Server is shutting down" });
            string httpResponse = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n{responseBody}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(httpResponse));
        });

        using IpcClient client = new(_socketPath);
        HttpResponseMessage response = await client.PostAsync("/manage/stop", null);

        await serverTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task IpcClient_CanPutToUnixSocket()
    {
        if (OperatingSystem.IsWindows())
            return;

        Task serverTask = Task.Run(async () =>
        {
            using Socket accepted = await _listenSocket.AcceptAsync();
            using NetworkStream stream = new(accepted);

            byte[] buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer);

            string responseBody = JsonSerializer.Serialize(new { status = "ok", message = "Configuration updated" });
            string httpResponse = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n{responseBody}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(httpResponse));
        });

        using IpcClient client = new(_socketPath);
        StringContent body = new(
            JsonSerializer.Serialize(new { server_name = "TestServer" }),
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await client.PutAsync("/manage/config", body);

        await serverTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task IpcClient_ThrowsOnConnectionRefused_WhenNoServer()
    {
        if (OperatingSystem.IsWindows())
            return;

        string badPath = Path.Combine(Path.GetTempPath(), $"nomercy-nonexistent-{Guid.NewGuid():N}.sock");

        using IpcClient client = new(badPath);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.GetAsync("/manage/status"));
    }

    public void Dispose()
    {
        _listenSocket.Dispose();

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
    }
}
