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
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Contract tests for <c>api/v1/dashboard/storage</c> — the universal
/// driver-backed browser behind the "attach a storage backend" dashboard flow.
/// All three routes require the Moderator policy. Unlike most of the API,
/// <c>/list</c> and <c>/mkdir</c> deliberately answer runtime failures with
/// HTTP 200 and <c>{ ok: false, error }</c> rather than a 4xx/5xx — that is
/// the CURRENT contract this suite characterizes, not a bug to "fix" by
/// asserting a 4xx. The one exception is a request the driver can never serve:
/// an unscoped local driver asked for its own root is rejected up front, before
/// any storage call. Mkdir tests never reach a real <c>CreateDirectoryAsync</c>
/// call — every case here is rejected by the driver_id/path validation guard
/// before the controller resolves a driver.
/// </summary>
[Trait("Category", "DashboardStorageBrowser")]
public class StorageBrowserControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public StorageBrowserControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    [Fact]
    public async Task Probe_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/probe",
            new { type = "local", config = new { } }
        );

        response
            .StatusCode.Should()
            .BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Probe_MissingType_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/probe",
            new { config = new { } }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Probe_UnknownType_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/probe",
            new { type = "ftp", config = new { } }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Probe_MissingConfig_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/probe",
            new { type = "local" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Probe_NonNfsType_ReturnsOkTrueWithEmptyExports_WhenModerator()
    {
        // Only "nfs" reaches network/mount code; every other allowed type short-circuits
        // to ok=true with no exports, so this exercises the real endpoint without any
        // outbound connection.
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/probe",
            new { type = "local", config = new { } }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("ok").GetBoolean().Should().BeTrue();
        root.GetProperty("exports").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("exports").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Probe_NfsMissingServer_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/probe",
            new { type = "nfs", config = new { } }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/list",
            new { driver_id = Driver.SystemLocalDriverId.ToString(), path = "" }
        );

        response
            .StatusCode.Should()
            .BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task List_MissingDriverId_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/list",
            new { path = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_InvalidDriverIdFormat_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/list",
            new { driver_id = "not-a-valid-ulid", path = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_UnknownDriverId_Returns404()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/list",
            new { driver_id = Ulid.NewUlid().ToString(), path = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_UnscopedLocalDriverAtItsRoot_Returns400_BecauseItHasNoRoot()
    {
        // Production seeds the system-local driver with an empty rootPath, so it applies
        // no root restriction and has nothing of its own to enumerate — it stands for the
        // host filesystem, which dashboard/filesystem serves. Answering 200 here let the
        // path guard's rejection reach the picker as an empty folder.
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/list",
            new { driver_id = NoMercyApiFactory.UnscopedLocalDriverId.ToString(), path = "" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_KnownDriverBelowItsRoot_Returns200WithOkField_WhenModerator()
    {
        // Characterizes the rest of the contract: a resolvable driver with a real
        // sub-path always yields HTTP 200 with an "ok" flag, whatever the outcome of
        // the underlying (real, read-only) storage.List call happens to be on this host.
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/list",
            new
            {
                driver_id = Driver.SystemLocalDriverId.ToString(),
                path = "nomercy-no-such-subpath",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("ok", out JsonElement ok).Should().BeTrue();
        ok.ValueKind.Should().BeOneOf([JsonValueKind.True, JsonValueKind.False]);
        root.TryGetProperty("path", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Mkdir_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/mkdir",
            new { driver_id = Driver.SystemLocalDriverId.ToString(), path = "some/sub/path" }
        );

        response
            .StatusCode.Should()
            .BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Mkdir_MissingDriverId_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/mkdir",
            new { path = "some/sub/path" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mkdir_InvalidDriverIdFormat_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/mkdir",
            new { driver_id = "not-a-valid-ulid", path = "some/sub/path" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mkdir_MissingPath_Returns400()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/mkdir",
            new { driver_id = Driver.SystemLocalDriverId.ToString() }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mkdir_UnknownDriverId_Returns404()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            "/api/v1/dashboard/storage/mkdir",
            new { driver_id = Ulid.NewUlid().ToString(), path = "some/sub/path" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
