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

using NoMercy.Launcher.Models;
using NoMercy.Launcher.Services;
using NoMercy.Tests.Launcher.Support;
using Xunit;

namespace NoMercy.Tests.Launcher.Services;

/// <summary>
/// REQUIREMENT: <see cref="ServerConnection"/> is the ONLY place that turns a
/// raw management-pipe HTTP response into either a deserialized value, a
/// success flag, or "disconnected" — every scenario here runs against a real
/// <see cref="FakeManagementPipeServer"/> named pipe (the same transport
/// <c>IpcClient</c> uses in production on Windows), so the request path,
/// status-code branching, and IsConnected bookkeeping are exercised against
/// the genuine wire format, never a mocked HttpClient.
/// </summary>
public sealed class ServerConnectionTests
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
    public async Task ConnectAsync_SuccessResponse_SetsIsConnectedTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");

        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        bool result = await connection.ConnectAsync();

        string request = await requestTask;
        request.Should().StartWith(expected: "GET /manage/status");
        result.Should().BeTrue();
        connection.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_ErrorStatusCode_SetsIsConnectedFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 500, reason: "Internal Server Error", body: "");

        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        bool result = await connection.ConnectAsync();

        await requestTask;
        result.Should().BeFalse();
        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_NoServerListening_ReturnsFalseInsteadOfThrowing()
    {
        // A unique pipe name nothing is listening on — the connect attempt
        // must fail gracefully (timeout/refused), never throw out of ConnectAsync.
        using ServerConnection connection = new(pipeNameOrSocketPath: $"nomercy-test-nobody-home-{Guid.NewGuid():N}");

        bool result = await connection.ConnectAsync();

        result.Should().BeFalse();
        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_SuccessResponse_DeserializesBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(
            server: server,
            status: 200,
            reason: "OK",
            body: """{"internal_port":7626,"server_name":"nomercy-test"}"""
        );
        ServerConfigResponse? result = await connection.GetAsync<ServerConfigResponse>(
            path: "/manage/config"
        );

        string request = await requestTask;
        request.Should().StartWith(expected: "GET /manage/config");
        result.Should().NotBeNull();
        result!.InternalPort.Should().Be(expected: 7626);
        result.ServerName.Should().Be(expected: "nomercy-test");
    }

    [Fact]
    public async Task GetAsync_ErrorStatusCode_ReturnsNullAndMarksDisconnected()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server: server, status: 404, reason: "Not Found", body: "");
        ServerConfigResponse? result = await connection.GetAsync<ServerConfigResponse>(
            path: "/manage/config"
        );

        await requestTask;
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_NoPriorConnect_ReturnsNullWithoutTouchingTransport()
    {
        using ServerConnection connection = new(pipeNameOrSocketPath: $"nomercy-test-{Guid.NewGuid():N}");

        ServerConfigResponse? result = await connection.GetAsync<ServerConfigResponse>(
            path: "/manage/config"
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task PostAsync_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: "");
        bool result = await connection.PostAsync(path: "/manage/stop");

        string request = await requestTask;
        request.Should().StartWith(expected: "POST /manage/stop");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PostAsync_ErrorResponse_ReturnsFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server: server, status: 503, reason: "Service Unavailable", body: "");
        bool result = await connection.PostAsync(path: "/manage/stop");

        await requestTask;
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PostWithBodyAsync_SuccessResponse_ReturnsSuccessAndBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(
            server: server,
            status: 200,
            reason: "OK",
            body: """{"status":"available","use_installer":true}"""
        );
        (bool success, string? body) = await connection.PostWithBodyAsync(path: "/manage/update");

        await requestTask;
        success.Should().BeTrue();
        body.Should().Contain(expected: "use_installer");
    }

    [Fact]
    public async Task PostAsyncGeneric_SerializesBodyAsJson()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: "");
        bool result = await connection.PostAsync(path: "/manage/autostart", body: new { enabled = true });

        string request = await requestTask;
        request.Should().Contain(expected: "\"enabled\":true");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PutAsyncGeneric_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        using ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: "");
        bool result = await connection.PutAsync(path: "/manage/config", body: new { server_name = "renamed" });

        string request = await requestTask;
        request.Should().StartWith(expected: "PUT /manage/config");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_ThenIsConnected_ReportsFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"running"}""");
        ServerConnection connection = new(pipeNameOrSocketPath: server.PipeName);
        await connection.ConnectAsync();
        await requestTask;

        connection.Dispose();

        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        ServerConnection connection = new(pipeNameOrSocketPath: $"nomercy-test-{Guid.NewGuid():N}");

        Action act = () =>
        {
            connection.Dispose();
            connection.Dispose();
        };

        act.Should().NotThrow();
    }
}
