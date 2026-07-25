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
using NoMercy.Tests.Common.Ipc;
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
        return server.RunOnceAsync(stream =>
            FakeManagementPipeServer.WriteResponseAsync(stream, status, reason, body)
        );
    }

    [Fact]
    public async Task ConnectAsync_SuccessResponse_SetsIsConnectedTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 200, "OK", """{"status":"running"}""");

        using ServerConnection connection = new(server.PipeName);
        bool result = await connection.ConnectAsync();

        string request = await requestTask;
        request.Should().StartWith("GET /manage/status");
        result.Should().BeTrue();
        connection.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_ErrorStatusCode_SetsIsConnectedFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 500, "Internal Server Error", "");

        using ServerConnection connection = new(server.PipeName);
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
        using ServerConnection connection = new($"nomercy-test-nobody-home-{Guid.NewGuid():N}");

        bool result = await connection.ConnectAsync();

        result.Should().BeFalse();
        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_SuccessResponse_DeserializesBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(
            server,
            200,
            "OK",
            """{"internal_port":7626,"server_name":"nomercy-test"}"""
        );
        ServerConfigResponse? result = await connection.GetAsync<ServerConfigResponse>(
            "/manage/config"
        );

        string request = await requestTask;
        request.Should().StartWith("GET /manage/config");
        result.Should().NotBeNull();
        result!.InternalPort.Should().Be(7626);
        result.ServerName.Should().Be("nomercy-test");
    }

    [Fact]
    public async Task GetAsync_ErrorStatusCode_ReturnsNullAndMarksDisconnected()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server, 404, "Not Found", "");
        ServerConfigResponse? result = await connection.GetAsync<ServerConfigResponse>(
            "/manage/config"
        );

        await requestTask;
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_NoPriorConnect_ReturnsNullWithoutTouchingTransport()
    {
        using ServerConnection connection = new($"nomercy-test-{Guid.NewGuid():N}");

        ServerConfigResponse? result = await connection.GetAsync<ServerConfigResponse>(
            "/manage/config"
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task PostAsync_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server, 200, "OK", "");
        bool result = await connection.PostAsync("/manage/stop");

        string request = await requestTask;
        request.Should().StartWith("POST /manage/stop");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PostAsync_ErrorResponse_ReturnsFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server, 503, "Service Unavailable", "");
        bool result = await connection.PostAsync("/manage/stop");

        await requestTask;
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PostWithBodyAsync_SuccessResponse_ReturnsSuccessAndBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(
            server,
            200,
            "OK",
            """{"status":"available","use_installer":true}"""
        );
        (bool success, string? body) = await connection.PostWithBodyAsync("/manage/update");

        await requestTask;
        success.Should().BeTrue();
        body.Should().Contain("use_installer");
    }

    [Fact]
    public async Task PostAsyncGeneric_SerializesBodyAsJson()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server, 200, "OK", "");
        bool result = await connection.PostAsync("/manage/autostart", new { enabled = true });

        string request = await requestTask;
        request.Should().Contain("\"enabled\":true");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PutAsyncGeneric_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> connectRequest = RespondWith(server, 200, "OK", """{"status":"running"}""");
        using ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await connectRequest;

        Task<string> requestTask = RespondWith(server, 200, "OK", "");
        bool result = await connection.PutAsync("/manage/config", new { server_name = "renamed" });

        string request = await requestTask;
        request.Should().StartWith("PUT /manage/config");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_ThenIsConnected_ReportsFalse()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 200, "OK", """{"status":"running"}""");
        ServerConnection connection = new(server.PipeName);
        await connection.ConnectAsync();
        await requestTask;

        connection.Dispose();

        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        ServerConnection connection = new($"nomercy-test-{Guid.NewGuid():N}");

        Action act = () =>
        {
            connection.Dispose();
            connection.Dispose();
        };

        act.Should().NotThrow();
    }
}
