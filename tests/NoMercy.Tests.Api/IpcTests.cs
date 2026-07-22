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

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Unit")]
public class IpcClientTests
{
    [Fact]
    public void IpcClient_CanBeCreated_WithDefaults()
    {
        using IpcClient client = new();

        Assert.NotNull(@object: client);
    }

    [Fact]
    public void IpcClient_CanBeCreated_WithCustomPath()
    {
        using IpcClient client = new(pipeNameOrSocketPath: "/tmp/test-nomercy.sock");

        Assert.NotNull(@object: client);
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
        Assert.Equal(expected: "NoMercyManagement", actual: Config.ManagementPipeName);
    }

    [Fact]
    public void Config_ManagementPipeName_CanBeSet()
    {
        string original = Config.ManagementPipeName;
        try
        {
            Config.ManagementPipeName = "TestPipe";
            Assert.Equal(expected: "TestPipe", actual: Config.ManagementPipeName);
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

        Assert.StartsWith(expectedStartString: AppFiles.AppPath, actualString: socketPath);
        Assert.EndsWith(expectedEndString: ".sock", actualString: socketPath);
    }
}

[Trait(name: "Category", value: "Integration")]
public class IpcUnixSocketIntegrationTests : IDisposable
{
    private readonly string _socketPath;
    private readonly Socket _listenSocket;

    public IpcUnixSocketIntegrationTests()
    {
        _socketPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nomercy-test-{Guid.NewGuid():N}.sock");
        _listenSocket = new(addressFamily: AddressFamily.Unix, socketType: SocketType.Stream, protocolType: ProtocolType.Unspecified);
        _listenSocket.Bind(localEP: new UnixDomainSocketEndPoint(path: _socketPath));
        _listenSocket.Listen(backlog: 1);
    }

    [Fact]
    public async Task IpcClient_ConnectsToUnixSocket_AndSendsRequest()
    {
        if (OperatingSystem.IsWindows())
            return; // Unix sockets only on Linux/macOS

        // Arrange — fake HTTP server on the socket
        Task<string> serverTask = Task.Run(function: async () =>
        {
            using Socket accepted = await _listenSocket.AcceptAsync();
            await using NetworkStream stream = new(socket: accepted);

            byte[] buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer: buffer);
            string request = Encoding.UTF8.GetString(bytes: buffer, index: 0, count: bytesRead);

            string responseBody = JsonSerializer.Serialize(value: new { status = "running" });
            string httpResponse =
                $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n{responseBody}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(s: httpResponse);
            await stream.WriteAsync(buffer: responseBytes);

            return request;
        });

        // Act
        using IpcClient client = new(pipeNameOrSocketPath: _socketPath);
        HttpResponseMessage response = await client.GetAsync(requestUri: "/manage/status");

        // Assert
        string receivedRequest = await serverTask;
        Assert.Contains(expectedSubstring: "GET /manage/status", actualString: receivedRequest);
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);
        Assert.Equal(expected: "running", actual: json.RootElement.GetProperty(propertyName: "status").GetString());
    }

    [Fact]
    public async Task IpcClient_CanPostToUnixSocket()
    {
        if (OperatingSystem.IsWindows())
            return;

        Task serverTask = Task.Run(function: async () =>
        {
            using Socket accepted = await _listenSocket.AcceptAsync();
            await using NetworkStream stream = new(socket: accepted);

            byte[] buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer: buffer);

            string responseBody = JsonSerializer.Serialize(
                value: new { status = "ok", message = "Server is shutting down" }
            );
            string httpResponse =
                $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n{responseBody}";
            await stream.WriteAsync(buffer: Encoding.UTF8.GetBytes(s: httpResponse));
        });

        using IpcClient client = new(pipeNameOrSocketPath: _socketPath);
        HttpResponseMessage response = await client.PostAsync(requestUri: "/manage/stop", content: null);

        await serverTask;
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: content);
        Assert.Equal(expected: "ok", actual: json.RootElement.GetProperty(propertyName: "status").GetString());
    }

    [Fact]
    public async Task IpcClient_CanPutToUnixSocket()
    {
        if (OperatingSystem.IsWindows())
            return;

        Task serverTask = Task.Run(function: async () =>
        {
            using Socket accepted = await _listenSocket.AcceptAsync();
            await using NetworkStream stream = new(socket: accepted);

            byte[] buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer: buffer);

            string responseBody = JsonSerializer.Serialize(
                value: new { status = "ok", message = "Configuration updated" }
            );
            string httpResponse =
                $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBody.Length}\r\nConnection: close\r\n\r\n{responseBody}";
            await stream.WriteAsync(buffer: Encoding.UTF8.GetBytes(s: httpResponse));
        });

        using IpcClient client = new(pipeNameOrSocketPath: _socketPath);
        StringContent body = new(
            content: JsonSerializer.Serialize(value: new { server_name = "TestServer" }),
            encoding: Encoding.UTF8,
            mediaType: "application/json"
        );
        HttpResponseMessage response = await client.PutAsync(requestUri: "/manage/config", content: body);

        await serverTask;
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task IpcClient_ThrowsOnConnectionRefused_WhenNoServer()
    {
        if (OperatingSystem.IsWindows())
            return;

        string badPath = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"nomercy-nonexistent-{Guid.NewGuid():N}.sock"
        );

        using IpcClient client = new(pipeNameOrSocketPath: badPath);

        await Assert.ThrowsAsync<HttpRequestException>(testCode: async () =>
            await client.GetAsync(requestUri: "/manage/status")
        );
    }

    public void Dispose()
    {
        _listenSocket.Dispose();

        if (File.Exists(path: _socketPath))
            File.Delete(path: _socketPath);
    }
}
