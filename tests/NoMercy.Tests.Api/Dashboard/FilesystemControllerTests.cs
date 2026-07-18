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
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Contract tests for <c>api/v1/dashboard/filesystem</c> — the folder-picker
/// backing the dashboard. All four routes require the Moderator policy.
/// Mkdir tests are restricted to input-validation branches (empty parent/name,
/// invalid filename characters) so no test ever creates a real directory on
/// the host — every asserted 400 here is thrown by <c>FilesystemRepository.Mkdir</c>
/// before it touches <c>IStorageDriver</c>.
/// </summary>
[Trait("Category", "DashboardFilesystem")]
public class FilesystemControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public FilesystemControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task List_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/ls",
            new { folder = "" }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_EmptyFolder_ReturnsOkWithNullParentAndEmptyData_WhenModerator()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/ls",
            new { folder = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("parent").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Home_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/home",
            new { }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Home_ReturnsOkWithStatusAndPath_WhenModerator()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/home",
            new { }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("ok");
        root.TryGetProperty("path", out JsonElement path).Should().BeTrue();
        path.ValueKind.Should().Be(JsonValueKind.String);
        path.GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Roots_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/roots",
            new { }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Roots_ReturnsOkWithStatusOkAndDataArray_WhenModerator()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/roots",
            new { }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("path").GetString().Should().Be("roots");
        root.GetProperty("parent").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);

        foreach (JsonElement entry in root.GetProperty("data").EnumerateArray())
        {
            entry.TryGetProperty("path", out _).Should().BeTrue();
            entry.TryGetProperty("full_path", out _).Should().BeTrue();
            entry.TryGetProperty("type", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Mkdir_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/mkdir",
            new { parent = "", name = "" }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Mkdir_MissingParent_Returns400_WithoutTouchingHostFilesystem()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/mkdir",
            new { parent = "", name = "new-folder" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mkdir_MissingName_Returns400_WithoutTouchingHostFilesystem()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/mkdir",
            new { parent = "/tmp", name = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mkdir_NameHasInvalidCharacters_Returns400_WithoutTouchingHostFilesystem()
    {
        // '/' is invalid in Path.GetInvalidFileNameChars() on every OS (unlike ':' or '*',
        // which are Windows-only), so this never falls through to a real CreateDirectory call.
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/filesystem/mkdir",
            new { parent = "/tmp", name = "bad/name" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
