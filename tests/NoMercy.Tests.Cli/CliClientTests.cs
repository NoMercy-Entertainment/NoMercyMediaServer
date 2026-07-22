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
[Trait(name: "Category", value: "Unit")]
public sealed class CliClientTests
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
    public async Task GetAsync_SuccessResponse_DeserializesBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(
            server: server,
            status: 200,
            reason: "OK",
            body: """{"status":"running","server_name":"nomercy-test"}"""
        );

        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        StatusResponse? result = await client.GetAsync<StatusResponse>(path: "/manage/status");

        string request = await requestTask;
        request.Should().StartWith(expected: "GET /manage/status");
        result.Should().NotBeNull();
        result!.Status.Should().Be(expected: "running");
        result.ServerName.Should().Be(expected: "nomercy-test");
    }

    [Fact]
    public async Task GetAsync_ErrorResponseWithBody_ReturnsNull_AndPrintsStatusAndBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(
            server: server,
            status: 404,
            reason: "Not Found",
            body: """{"error":"no such route"}"""
        );

        using ConsoleCapture console = new();
        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        StatusResponse? result = await client.GetAsync<StatusResponse>(path: "/manage/missing");

        await requestTask;
        result.Should().BeNull();
        console.Error.Should().Contain(expected: "Error: 404 Not Found");
        console.Error.Should().Contain(expected: "no such route");
    }

    [Fact]
    public async Task GetAsync_ErrorResponseWithEmptyBody_PrintsOnlyStatusLine()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 500, reason: "Internal Server Error", body: "");

        using ConsoleCapture console = new();
        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        StatusResponse? result = await client.GetAsync<StatusResponse>(path: "/manage/status");

        await requestTask;
        result.Should().BeNull();
        string[] lines = console
            .Error.ToString()
            .Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().ContainSingle();
        lines[0].Should().Contain(expected: "Error: 500");
    }

    [Fact]
    public async Task GetRawAsync_SuccessResponse_ReturnsRawBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: "plain-text-body");

        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        string? result = await client.GetRawAsync(path: "/manage/status");

        await requestTask;
        result.Should().Be(expected: "plain-text-body");
    }

    [Fact]
    public async Task GetRawAsync_ErrorResponse_ReturnsNull_AndPrintsStatusLine()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 503, reason: "Service Unavailable", body: "down");

        using ConsoleCapture console = new();
        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        string? result = await client.GetRawAsync(path: "/manage/status");

        await requestTask;
        result.Should().BeNull();
        console.Error.Should().Contain(expected: "Error: 503 Service Unavailable");
    }

    [Fact]
    public async Task PostAsync_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"ok"}""");

        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        bool result = await client.PostAsync(path: "/manage/stop");

        string request = await requestTask;
        request.Should().StartWith(expected: "POST /manage/stop");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PostAsync_ErrorResponseWithBody_ReturnsFalse_AndPrintsBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 400, reason: "Bad Request", body: "malformed request");

        using ConsoleCapture console = new();
        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        bool result = await client.PostAsync(path: "/manage/stop");

        await requestTask;
        result.Should().BeFalse();
        console.Error.Should().Contain(expected: "Error: 400 Bad Request");
        console.Error.Should().Contain(expected: "malformed request");
    }

    [Fact]
    public async Task PostAsyncGeneric_SuccessResponse_DeserializesBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(
            server: server,
            status: 200,
            reason: "OK",
            body: """{"status":"ok","message":"Update downloaded"}"""
        );

        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        UpdateStatusResponse? result = await client.PostAsync<UpdateStatusResponse>(
            path: "/manage/update"
        );

        await requestTask;
        result.Should().NotBeNull();
        result!.Status.Should().Be(expected: "ok");
        result.Message.Should().Be(expected: "Update downloaded");
    }

    [Fact]
    public async Task PostAsyncGeneric_ErrorResponseWithBody_ReturnsNull_AndPrintsBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 409, reason: "Conflict", body: "update already staged");

        using ConsoleCapture console = new();
        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        UpdateStatusResponse? result = await client.PostAsync<UpdateStatusResponse>(
            path: "/manage/update"
        );

        await requestTask;
        result.Should().BeNull();
        console.Error.Should().Contain(expected: "Error: 409 Conflict");
        console.Error.Should().Contain(expected: "update already staged");
    }

    [Fact]
    public async Task PostAsyncGeneric_ErrorResponseWithEmptyBody_ReturnsNull_WithoutBodyLine()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 500, reason: "Internal Server Error", body: "");

        using ConsoleCapture console = new();
        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        UpdateStatusResponse? result = await client.PostAsync<UpdateStatusResponse>(
            path: "/manage/update"
        );

        await requestTask;
        result.Should().BeNull();
        string[] lines = console
            .Error.ToString()
            .Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().ContainSingle();
    }

    [Fact]
    public async Task PutAsync_SuccessResponse_ReturnsTrue()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 200, reason: "OK", body: """{"status":"ok"}""");

        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        bool result = await client.PutAsync(path: "/manage/config");

        string request = await requestTask;
        request.Should().StartWith(expected: "PUT /manage/config");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PutAsync_ErrorResponseWithBody_ReturnsFalse_AndPrintsBody()
    {
        FakeManagementPipeServer server = new();
        Task<string> requestTask = RespondWith(server: server, status: 422, reason: "Unprocessable Entity", body: "bad key");

        using ConsoleCapture console = new();
        using CliClient client = new(pipeNameOrSocketPath: server.PipeName);
        bool result = await client.PutAsync(path: "/manage/config");

        await requestTask;
        result.Should().BeFalse();
        console.Error.Should().Contain(expected: "Error: 422 Unprocessable Entity");
        console.Error.Should().Contain(expected: "bad key");
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        CliClient client = new(pipeNameOrSocketPath: "nomercy-test-dispose-pipe");

        Exception? ex = Record.Exception(testCode: () =>
        {
            client.Dispose();
            client.Dispose();
        });

        ex.Should().BeNull();
    }

    private sealed class UpdateStatusResponse
    {
        [JsonProperty(propertyName: "status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty(propertyName: "message")]
        public string Message { get; set; } = string.Empty;
    }
}
