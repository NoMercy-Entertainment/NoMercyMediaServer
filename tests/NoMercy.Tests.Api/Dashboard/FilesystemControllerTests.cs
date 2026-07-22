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
[Trait(name: "Category", value: "DashboardFilesystem")]
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
            requestUri: "/api/v1/dashboard/filesystem/ls",
            value: new { folder = "" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task List_EmptyFolder_ReturnsOkWithNullParentAndEmptyData_WhenModerator()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/ls",
            value: new { folder = "" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "status").GetString().Should().Be(expected: "ok");
        root.GetProperty(propertyName: "parent").ValueKind.Should().Be(expected: JsonValueKind.Null);
        root.GetProperty(propertyName: "data").ValueKind.Should().Be(expected: JsonValueKind.Array);
        root.GetProperty(propertyName: "data").GetArrayLength().Should().Be(expected: 0);
    }

    [Fact]
    public async Task Home_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/home",
            value: new { }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Home_ReturnsOkWithStatusAndPath_WhenModerator()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/home",
            value: new { }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "status").GetString().Should().Be(expected: "ok");
        root.TryGetProperty(propertyName: "path", value: out JsonElement path).Should().BeTrue();
        path.ValueKind.Should().Be(expected: JsonValueKind.String);
        path.GetString().Should().NotBeNullOrEmpty();
        root.GetProperty(propertyName: "data").ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task Roots_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/roots",
            value: new { }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Roots_ReturnsOkWithStatusOkAndDataArray_WhenModerator()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/roots",
            value: new { }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "status").GetString().Should().Be(expected: "ok");
        root.GetProperty(propertyName: "path").GetString().Should().Be(expected: "roots");
        root.GetProperty(propertyName: "parent").ValueKind.Should().Be(expected: JsonValueKind.Null);
        root.GetProperty(propertyName: "data").ValueKind.Should().Be(expected: JsonValueKind.Array);

        foreach (JsonElement entry in root.GetProperty(propertyName: "data").EnumerateArray())
        {
            entry.TryGetProperty(propertyName: "path", value: out _).Should().BeTrue();
            entry.TryGetProperty(propertyName: "full_path", value: out _).Should().BeTrue();
            entry.TryGetProperty(propertyName: "type", value: out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Mkdir_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/mkdir",
            value: new { parent = "", name = "" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Mkdir_MissingParent_Returns400_WithoutTouchingHostFilesystem()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/mkdir",
            value: new { parent = "", name = "new-folder" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mkdir_MissingName_Returns400_WithoutTouchingHostFilesystem()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/mkdir",
            value: new { parent = "/tmp", name = "" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mkdir_NameHasInvalidCharacters_Returns400_WithoutTouchingHostFilesystem()
    {
        // '/' is invalid in Path.GetInvalidFileNameChars() on every OS (unlike ':' or '*',
        // which are Windows-only), so this never falls through to a real CreateDirectory call.
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: "/api/v1/dashboard/filesystem/mkdir",
            value: new { parent = "/tmp", name = "bad/name" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }
}
