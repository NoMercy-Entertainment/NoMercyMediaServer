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

using Newtonsoft.Json;
using NoMercy.Cli;
using NoMercy.Cli.Models;
using NoMercy.Tests.Cli.Support;
using NoMercy.Tests.Common.Ipc;
using Xunit;

namespace NoMercy.Tests.Cli;

/// <summary>
/// REQUIREMENT: <c>CliClient</c> is the only place that turns a raw management
/// HTTP response into either a deserialized value or a user-visible error line
/// on stderr. Every scenario here runs against a real <see cref="FakeManagementPipeServer"/>
/// named pipe — the same transport <c>IpcClient</c> uses in production on
/// Windows — so a regression in the success/error/empty-body branching is
/// caught against the real wire format, not a mock of the client under test.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CliClientTests
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
    public async Task GetAsync_SuccessResponse_DeserializesBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(
            server,
            200,
            "OK",
            """{"status":"running","server_name":"nomercy-test"}"""
        );

        using CliClient client = new(server.PipeName);
        StatusResponse? result = await client.GetAsync<StatusResponse>("/manage/status");

        string request = await requestTask;
        request.Should().StartWith("GET /manage/status");
        result.Should().NotBeNull();
        result!.Status.Should().Be("running");
        result.ServerName.Should().Be("nomercy-test");
    }

    [Fact]
    public async Task GetAsync_ErrorResponseWithBody_ReturnsNull_AndPrintsStatusAndBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(
            server,
            404,
            "Not Found",
            """{"error":"no such route"}"""
        );

        using ConsoleCapture console = new();
        using CliClient client = new(server.PipeName);
        StatusResponse? result = await client.GetAsync<StatusResponse>("/manage/missing");

        await requestTask;
        result.Should().BeNull();
        console.Error.Should().Contain("Error: 404 Not Found");
        console.Error.Should().Contain("no such route");
    }

    [Fact]
    public async Task GetAsync_ErrorResponseWithEmptyBody_PrintsOnlyStatusLine()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 500, "Internal Server Error", "");

        using ConsoleCapture console = new();
        using CliClient client = new(server.PipeName);
        StatusResponse? result = await client.GetAsync<StatusResponse>("/manage/status");

        await requestTask;
        result.Should().BeNull();
        string[] lines = console
            .Error.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().ContainSingle();
        lines[0].Should().Contain("Error: 500");
    }

    [Fact]
    public async Task GetRawAsync_SuccessResponse_ReturnsRawBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 200, "OK", "plain-text-body");

        using CliClient client = new(server.PipeName);
        string? result = await client.GetRawAsync("/manage/status");

        await requestTask;
        result.Should().Be("plain-text-body");
    }

    [Fact]
    public async Task GetRawAsync_ErrorResponse_ReturnsNull_AndPrintsStatusLine()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 503, "Service Unavailable", "down");

        using ConsoleCapture console = new();
        using CliClient client = new(server.PipeName);
        string? result = await client.GetRawAsync("/manage/status");

        await requestTask;
        result.Should().BeNull();
        console.Error.Should().Contain("Error: 503 Service Unavailable");
    }

    [Fact]
    public async Task PostAsync_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 200, "OK", """{"status":"ok"}""");

        using CliClient client = new(server.PipeName);
        bool result = await client.PostAsync("/manage/stop");

        string request = await requestTask;
        request.Should().StartWith("POST /manage/stop");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PostAsync_ErrorResponseWithBody_ReturnsFalse_AndPrintsBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 400, "Bad Request", "malformed request");

        using ConsoleCapture console = new();
        using CliClient client = new(server.PipeName);
        bool result = await client.PostAsync("/manage/stop");

        await requestTask;
        result.Should().BeFalse();
        console.Error.Should().Contain("Error: 400 Bad Request");
        console.Error.Should().Contain("malformed request");
    }

    [Fact]
    public async Task PostAsyncGeneric_SuccessResponse_DeserializesBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(
            server,
            200,
            "OK",
            """{"status":"ok","message":"Update downloaded"}"""
        );

        using CliClient client = new(server.PipeName);
        UpdateStatusResponse? result = await client.PostAsync<UpdateStatusResponse>(
            "/manage/update"
        );

        await requestTask;
        result.Should().NotBeNull();
        result!.Status.Should().Be("ok");
        result.Message.Should().Be("Update downloaded");
    }

    [Fact]
    public async Task PostAsyncGeneric_ErrorResponseWithBody_ReturnsNull_AndPrintsBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 409, "Conflict", "update already staged");

        using ConsoleCapture console = new();
        using CliClient client = new(server.PipeName);
        UpdateStatusResponse? result = await client.PostAsync<UpdateStatusResponse>(
            "/manage/update"
        );

        await requestTask;
        result.Should().BeNull();
        console.Error.Should().Contain("Error: 409 Conflict");
        console.Error.Should().Contain("update already staged");
    }

    [Fact]
    public async Task PostAsyncGeneric_ErrorResponseWithEmptyBody_ReturnsNull_WithoutBodyLine()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 500, "Internal Server Error", "");

        using ConsoleCapture console = new();
        using CliClient client = new(server.PipeName);
        UpdateStatusResponse? result = await client.PostAsync<UpdateStatusResponse>(
            "/manage/update"
        );

        await requestTask;
        result.Should().BeNull();
        string[] lines = console
            .Error.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().ContainSingle();
    }

    [Fact]
    public async Task PutAsync_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 200, "OK", """{"status":"ok"}""");

        using CliClient client = new(server.PipeName);
        bool result = await client.PutAsync("/manage/config");

        string request = await requestTask;
        request.Should().StartWith("PUT /manage/config");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PutAsync_ErrorResponseWithBody_ReturnsFalse_AndPrintsBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server, 422, "Unprocessable Entity", "bad key");

        using ConsoleCapture console = new();
        using CliClient client = new(server.PipeName);
        bool result = await client.PutAsync("/manage/config");

        await requestTask;
        result.Should().BeFalse();
        console.Error.Should().Contain("Error: 422 Unprocessable Entity");
        console.Error.Should().Contain("bad key");
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        CliClient client = new("nomercy-test-dispose-pipe");

        Exception? ex = Record.Exception(() =>
        {
            client.Dispose();
            client.Dispose();
        });

        ex.Should().BeNull();
    }

    private sealed class UpdateStatusResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }
}
