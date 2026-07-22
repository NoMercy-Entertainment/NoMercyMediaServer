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
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

// Locks the CURRENT contract of DriversController: Moderator-gated reads
// (Index/GetTypes/Show/GetSystemLocalId) and Owner-gated mutations
// (Create/Update/UpdateCredentials/Delete). SecondaryUserId (Allowed=true,
// Owner=false, Manage=false) proves rejection at BOTH gates — it is neither
// a moderator nor an owner. Mutations are exercised only for their auth pair
// and validation short-circuit paths (never their success path) so no real
// Driver row or credential is ever written by this suite.
[Trait(name: "Category", value: "DashboardDrivers")]
public class DriversControllerTests : IClassFixture<NoMercyApiFactory>
{
    private const string BaseUrl = "/api/v1/dashboard/drivers";

    private static readonly string SystemLocalId = Driver.SystemLocalDriverId.ToString();

    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    public DriversControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    [Fact]
    public async Task Index_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: BaseUrl);

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Index_ReturnsForbidden_WhenSecondaryUserNotModerator()
    {
        HttpResponseMessage response = await _secondaryUser.GetAsync(requestUri: BaseUrl);

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Index_ReturnsArrayIncludingSeededSystemDriver_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: BaseUrl);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        doc.RootElement.ValueKind.Should()
            .Be(expected: JsonValueKind.Array, because: "Index returns a raw array, not a data envelope");

        JsonElement systemDriver = doc
            .RootElement.EnumerateArray()
            .First(predicate: e => e.GetProperty(propertyName: "id").GetString() == SystemLocalId);

        systemDriver.GetProperty(propertyName: "type").GetString().Should().Be(expected: "local");
        systemDriver.GetProperty(propertyName: "is_system").GetBoolean().Should().BeTrue();
        systemDriver
            .TryGetProperty(propertyName: "credentials_configured", value: out JsonElement credsConfigured)
            .Should()
            .BeTrue();
        credsConfigured.GetBoolean().Should().BeFalse();
        systemDriver.TryGetProperty(propertyName: "folder_count", value: out _).Should().BeTrue();
        systemDriver.TryGetProperty(propertyName: "created_at", value: out _).Should().BeTrue();
        systemDriver.TryGetProperty(propertyName: "updated_at", value: out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetTypes_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: $"{BaseUrl}/types");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetTypes_ReturnsForbidden_WhenSecondaryUserNotModerator()
    {
        HttpResponseMessage response = await _secondaryUser.GetAsync(requestUri: $"{BaseUrl}/types");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetTypes_ReturnsAllFiveDriverTypeEntries_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"{BaseUrl}/types");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        doc.RootElement.ValueKind.Should().Be(expected: JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(expected: 5);

        string[] types = doc
            .RootElement.EnumerateArray()
            .Select(selector: e => e.GetProperty(propertyName: "type").GetString()!)
            .ToArray();

        types.Should().BeEquivalentTo(expectation: ["local", "nfs", "s3", "r2", "webdav"]);
    }

    [Fact]
    public async Task Show_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: $"{BaseUrl}/{SystemLocalId}");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Show_ReturnsForbidden_WhenSecondaryUserNotModerator()
    {
        HttpResponseMessage response = await _secondaryUser.GetAsync(requestUri: $"{BaseUrl}/{SystemLocalId}");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Show_ReturnsSystemLocalDriverEnvelope_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"{BaseUrl}/{SystemLocalId}");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        doc.RootElement.GetProperty(propertyName: "id").GetString().Should().Be(expected: SystemLocalId);
        doc.RootElement.GetProperty(propertyName: "type").GetString().Should().Be(expected: "local");
        doc.RootElement.GetProperty(propertyName: "is_system").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Show_ReturnsNotFound_WhenDriverDoesNotExist()
    {
        Ulid unknownId = Ulid.NewUlid();

        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"{BaseUrl}/{unknownId}");

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSystemLocalId_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: $"{BaseUrl}/system-local");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetSystemLocalId_ReturnsForbidden_WhenSecondaryUserNotModerator()
    {
        HttpResponseMessage response = await _secondaryUser.GetAsync(requestUri: $"{BaseUrl}/system-local");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetSystemLocalId_ReturnsStableSystemDriverId_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: $"{BaseUrl}/system-local");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        doc.RootElement.GetProperty(propertyName: "id").GetString().Should().Be(expected: SystemLocalId);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsJsonAsync(
            requestUri: BaseUrl,
            value: new { name = "contract-test-driver", type = "local" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Create_ReturnsForbidden_WhenSecondaryUserNotOwner()
    {
        HttpResponseMessage response = await _secondaryUser.PostAsJsonAsync(
            requestUri: BaseUrl,
            value: new { name = "contract-test-driver", type = "local" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTypeInvalid()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: BaseUrl,
            value: new { name = "contract-test-driver", type = "ftp" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenLocalConfigMissingRootPath()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: BaseUrl,
            value: new { name = "contract-test-driver", type = "local" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameMissing()
    {
        HttpResponseMessage response = await _authed.PostAsJsonAsync(
            requestUri: BaseUrl,
            value: new
            {
                name = "",
                type = "local",
                config = new { rootPath = "/mnt/media" },
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{SystemLocalId}",
            value: new { name = "renamed" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Update_ReturnsForbidden_WhenSecondaryUserNotOwner()
    {
        HttpResponseMessage response = await _secondaryUser.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{SystemLocalId}",
            value: new { name = "renamed" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenDriverDoesNotExist()
    {
        Ulid unknownId = Ulid.NewUlid();

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{unknownId}",
            value: new { name = "renamed" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenTargetingBuiltInSystemLocalDriver()
    {
        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{SystemLocalId}",
            value: new { name = "renamed" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateCredentials_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{SystemLocalId}/credentials",
            value: new { access_key = "ak", secret_key = "sk" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task UpdateCredentials_ReturnsForbidden_WhenSecondaryUserNotOwner()
    {
        HttpResponseMessage response = await _secondaryUser.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{SystemLocalId}/credentials",
            value: new { access_key = "ak", secret_key = "sk" }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task UpdateCredentials_ReturnsNotFound_WhenDriverDoesNotExist()
    {
        Ulid unknownId = Ulid.NewUlid();

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{unknownId}/credentials",
            value: new { access_key = "ak", secret_key = "sk" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCredentials_ReturnsBadRequest_WhenBothFieldsBlank()
    {
        // Blank credentials short-circuit BEFORE CredentialManager.SetCredentials is
        // called, so this exercises validation only — no real credential is written.
        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"{BaseUrl}/{SystemLocalId}/credentials",
            value: new { access_key = "", secret_key = "" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(requestUri: $"{BaseUrl}/{SystemLocalId}");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenSecondaryUserNotOwner()
    {
        HttpResponseMessage response = await _secondaryUser.DeleteAsync(
            requestUri: $"{BaseUrl}/{SystemLocalId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenTargetingBuiltInSystemLocalDriver()
    {
        // The system-local guard fires before any repository lookup or delete —
        // no real row is ever touched by this assertion.
        HttpResponseMessage response = await _authed.DeleteAsync(requestUri: $"{BaseUrl}/{SystemLocalId}");

        response.StatusCode.Should().Be(expected: HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenDriverDoesNotExist()
    {
        Ulid unknownId = Ulid.NewUlid();

        HttpResponseMessage response = await _authed.DeleteAsync(requestUri: $"{BaseUrl}/{unknownId}");

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }
}
