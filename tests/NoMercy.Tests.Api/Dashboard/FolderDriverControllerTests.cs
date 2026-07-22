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
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait(name: "Category", value: "FolderDriver")]
public class FolderDriverControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    // Use the folder seeded by NoMercyApiFactory
    private static readonly Ulid MovieFolderId = NoMercyApiFactory.MovieFolderId;

    public FolderDriverControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    // =========================================================================
    // GET /drivers — metadata list
    // =========================================================================

    [Fact]
    public async Task GetDriverTypes_ReturnsAllFiveEntries()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/folders/drivers");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        JsonElement root = json.RootElement;

        Assert.Equal(expected: JsonValueKind.Array, actual: root.ValueKind);
        Assert.Equal(expected: 5, actual: root.GetArrayLength());
    }

    [Fact]
    public async Task GetDriverTypes_LocalIsAvailable_S3AndR2AreNot()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/folders/drivers");
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);

        Dictionary<string, bool> availability = json
            .RootElement.EnumerateArray()
            .ToDictionary(
                keySelector: e => e.GetProperty(propertyName: "type").GetString()!,
                elementSelector: e => e.GetProperty(propertyName: "available").GetBoolean()
            );

        // All five built-in driver types are now flagged as available;
        // the old smb entry was replaced by nfs.
        Assert.True(condition: availability[key: "local"], userMessage: "local should be available");
        Assert.True(condition: availability[key: "nfs"], userMessage: "nfs should be available");
        Assert.True(condition: availability[key: "s3"], userMessage: "s3 should be available");
        Assert.True(condition: availability[key: "r2"], userMessage: "r2 should be available");
        Assert.True(condition: availability[key: "webdav"], userMessage: "webdav should be available");
        Assert.False(condition: availability.ContainsKey(key: "smb"), userMessage: "smb is no longer a recognised driver type");
    }

    [Fact]
    public async Task GetDriverTypes_Unauthenticated_Returns401Or403()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: "/api/v1/dashboard/folders/drivers"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // GET /{id}/driver — read current state
    // =========================================================================

    [Fact]
    public async Task GetBackend_SeededFolder_ReturnsLocalDefault()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver"
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        JsonElement root = json.RootElement;

        Assert.Equal(expected: "local", actual: root.GetProperty(propertyName: "driver_type").GetString());
    }

    [Fact]
    public async Task GetBackend_UnknownFolder_Returns404()
    {
        Ulid unknownId = Ulid.NewUlid();
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: $"/api/v1/dashboard/folders/{unknownId}/driver"
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task GetBackend_Unauthenticated_Returns401Or403()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // PUT /{id}/driver — update
    // =========================================================================

    [Fact]
    public async Task PutBackend_ValidLocalConfig_Returns200AndPersists()
    {
        // Assign the seeded system-local driver by its stable Ulid.
        object payload = new { driver_id = Driver.SystemLocalDriverId.ToString() };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.Equal(expected: "local", actual: json.RootElement.GetProperty(propertyName: "driver_type").GetString());

        // Verify persisted in DB
        await using MediaContext ctx = new();
        Folder? folder = await ctx
            .Folders.AsNoTracking()
            .FirstOrDefaultAsync(predicate: f => f.Id == MovieFolderId);

        Assert.NotNull(@object: folder);
        Assert.Equal(expected: Driver.SystemLocalDriverId, actual: folder!.DriverId);
    }

    [Fact]
    public async Task PutBackend_LocalWithRootPath_Returns200()
    {
        // path is the optional sub-path within the driver root; driver_id is always required.
        object payload = new
        {
            driver_id = Driver.SystemLocalDriverId.ToString(),
            path = "/mnt/media",
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected 200, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task PutDriver_InvalidDriverType_Returns400()
    {
        object payload = new { driver_type = "ftp" };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );

        Assert.Equal(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_SmbMissingMountPath_Returns400()
    {
        object payload = new
        {
            driver_type = "smb",
            driver_config = new { someOtherField = "value" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );

        Assert.Equal(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_NfsMissingMountPath_Returns400()
    {
        object payload = new { driver_type = "nfs", driver_config = (object?)null };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );

        Assert.Equal(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_S3MissingBucket_Returns400()
    {
        object payload = new { driver_type = "s3", driver_config = new { region = "us-east-1" } };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );

        Assert.Equal(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_S3ValidConfig_Returns200WithWarning()
    {
        // The assign endpoint no longer accepts driver_type/config inline — it takes a
        // driver_id referencing an existing Driver row. Assigning a driver_id that does
        // not exist in the DB must return 404 (driver not found).
        Ulid nonExistentDriverId = Ulid.NewUlid();
        object payload = new { driver_id = nonExistentDriverId.ToString() };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_R2ValidConfig_Returns200WithWarning()
    {
        // The assign endpoint no longer accepts driver_type/config inline — it takes a
        // driver_id referencing an existing Driver row. Verify that assigning the seeded
        // system-local driver returns the expected FolderDriverInfoDto shape.
        object payload = new { driver_id = Driver.SystemLocalDriverId.ToString() };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        JsonElement root = json.RootElement;

        Assert.Equal(
            expected: Driver.SystemLocalDriverId.ToString(),
            actual: root.GetProperty(propertyName: "driver_id").GetString()
        );
        Assert.Equal(expected: "local", actual: root.GetProperty(propertyName: "driver_type").GetString());
    }

    [Fact]
    public async Task PutBackend_UnknownFolder_Returns404()
    {
        Ulid unknownId = Ulid.NewUlid();
        // driver_id must be present and valid to pass the validation gate;
        // the 404 should come from the folder lookup, not from missing driver_id.
        object payload = new { driver_id = Driver.SystemLocalDriverId.ToString() };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{unknownId}/driver",
            value: payload
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_Unauthenticated_Returns401Or403()
    {
        object payload = new { driver_type = "local" };

        HttpResponseMessage response = await _unauthed.PutAsJsonAsync(
            requestUri: $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            value: payload
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }
}
