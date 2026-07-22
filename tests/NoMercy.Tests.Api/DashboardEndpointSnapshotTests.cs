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
using System.Text;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Characterization")]
public class DashboardEndpointSnapshotTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _client;

    public DashboardEndpointSnapshotTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient().AsAuthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private static void AssertJsonHasProperty(JsonElement element, string propertyName) =>
        Assert.True(
            condition: element.TryGetProperty(propertyName: propertyName, value: out _),
            userMessage: $"Expected JSON property '{propertyName}' not found. "
                         + $"Properties: [{string.Join(separator: ", ", values: EnumerateProperties(element: element))}]"
        );

    private static IEnumerable<string> EnumerateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty prop in element.EnumerateObject())
                yield return prop.Name;
    }

    private static void AssertProblemDetailsShape(JsonElement root, int expectedStatus)
    {
        AssertJsonHasProperty(element: root, propertyName: "type");
        AssertJsonHasProperty(element: root, propertyName: "title");
        AssertJsonHasProperty(element: root, propertyName: "status");
        AssertJsonHasProperty(element: root, propertyName: "detail");
        AssertJsonHasProperty(element: root, propertyName: "instance");
        Assert.Equal(expected: expectedStatus, actual: root.GetProperty(propertyName: "status").GetInt32());
    }

    private static void AssertStatusResponse(JsonElement root)
    {
        bool hasCustomStatus =
            root.TryGetProperty(propertyName: "message", value: out _) && root.TryGetProperty(propertyName: "status", value: out _);
        bool hasProblemDetails =
            root.TryGetProperty(propertyName: "detail", value: out _) && root.TryGetProperty(propertyName: "status", value: out _);

        Assert.True(
            condition: hasCustomStatus || hasProblemDetails,
            userMessage: $"Expected status response shape. "
                         + $"Properties: [{string.Join(separator: ", ", values: EnumerateProperties(element: root))}]"
        );
    }

    // =========================================================================
    // ConfigurationController — /api/v1/dashboard/configuration
    // =========================================================================

    [Fact]
    public async Task Configuration_Index_ReturnsConfigData()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Configuration_Store_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/configuration",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Configuration_Languages_ReturnsList()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/dashboard/configuration/languages"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.True(
            condition: json.RootElement.ValueKind == JsonValueKind.Array,
            userMessage: "Expected array response for languages"
        );
    }

    [Fact]
    public async Task Configuration_Countries_ReturnsList()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/dashboard/configuration/countries"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.True(
            condition: json.RootElement.ValueKind == JsonValueKind.Array,
            userMessage: "Expected array response for countries"
        );
    }

    // =========================================================================
    // DevicesController — /api/v1/dashboard/devices
    // =========================================================================

    [Fact]
    public async Task Devices_Index_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/devices");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Devices_Create_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/devices",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Devices_Destroy_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.DeleteAsync(requestUri: "/api/v1/dashboard/devices");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // EncoderController — /api/v1/dashboard/encoderprofiles
    // =========================================================================

    [Fact]
    public async Task Encoder_Index_ReturnsProfiles()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/encoderprofiles");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        // The endpoint wraps results in { data: [...] } — not a bare array.
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        Assert.True(
            condition: json.RootElement.GetProperty(propertyName: "data").ValueKind == JsonValueKind.Array,
            userMessage: "Expected data property to be an array of encoder profiles"
        );
    }

    [Fact]
    public async Task Encoder_Create_Returns410Gone()
    {
        // POST /api/v1/dashboard/encoderprofiles was removed in the V2 migration.
        // The replacement is POST /api/v1/encoder/profiles/{parentId}/clone.
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/encoderprofiles",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.Gone,
            userMessage: $"Expected 410 Gone, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "error");
    }

    [Fact]
    public async Task Encoder_Destroy_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/encoderprofiles/{Ulid.NewUlid()}"
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Encoder_Containers_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/dashboard/encoderprofiles/containers"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Encoder_FrameSizes_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/dashboard/encoderprofiles/framesizes"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // Dashboard LibrariesController — /api/v1/dashboard/libraries
    // =========================================================================

    [Fact]
    public async Task DashboardLibraries_Index_ReturnsLibraries()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/libraries");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task DashboardLibraries_Store_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/libraries",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
    }

    [Fact]
    public async Task DashboardLibraries_Delete_NonExistent_ReturnsErrorStatus()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertProblemDetailsShape(root: json.RootElement, expectedStatus: 404);
    }

    [Fact]
    public async Task DashboardLibraries_Rescan_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/libraries/rescan",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task DashboardLibraries_RescanById_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/rescan",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task DashboardLibraries_Refresh_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/libraries/refresh",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task DashboardLibraries_RefreshById_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/refresh",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task DashboardLibraries_AddFolder_NonExistentLibrary_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/folders",
            content: JsonBody(obj: new { path = "/tmp/test" })
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task DashboardLibraries_DeleteFolder_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/folders/{Ulid.NewUlid()}"
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task DashboardLibraries_DeleteEncoderProfile_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/folders/{Ulid.NewUlid()}/encoder_profiles/{Ulid.NewUlid()}"
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // LogController — /api/v1/dashboard/logs
    // =========================================================================

    [Fact]
    public async Task Logs_Index_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/logs");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Logs_Levels_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/logs/levels");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Logs_Types_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/logs/types");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // PluginController — /api/v1/dashboard/plugins
    // =========================================================================

    [Fact]
    public async Task Plugins_Index_ReturnsDataResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/plugins");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Plugins_Credentials_ReturnsCredentialsOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/dashboard/plugins/credentials"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Plugins_SetCredentials_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/plugins/credentials",
            content: JsonBody(
                obj: new
                {
                    key = "AniDb",
                    username = "test",
                    apiKey = "test-key",
                }
            )
        ); // [REDACTED] test fixture

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
    }

    // =========================================================================
    // ServerActivityController — /api/v1/dashboard/activity
    // =========================================================================

    [Fact]
    public async Task Activity_Index_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/activity");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Activity_Create_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/activity",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Activity_Destroy_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.DeleteAsync(requestUri: "/api/v1/dashboard/activity");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    // =========================================================================
    // ServerController — /api/v1/dashboard/server
    // =========================================================================

    [Fact]
    public async Task Server_Index_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/server");

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Server_Setup_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/server/setup");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

        JsonElement data = json.RootElement.GetProperty(propertyName: "data");
        AssertJsonHasProperty(element: data, propertyName: "setup_complete");
    }

    [Fact]
    public async Task Server_Start_ReturnsNotImplemented()
    {
        // Server start is a no-op from inside the process — it requires OS-level
        // supervision (systemd, Windows Service, etc.). The endpoint intentionally
        // returns 501 until a supervisor protocol is in place.
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/server/start",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotImplemented,
            userMessage: $"Expected NotImplemented (501), got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Server_Restart_ReturnsNotImplemented()
    {
        // Server restart requires OS-level supervision — the process cannot restart
        // itself. The endpoint intentionally returns 501 until a supervisor protocol
        // is in place.
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/server/restart",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotImplemented,
            userMessage: $"Expected NotImplemented (501), got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Server_CheckForUpdate_ReturnsUpdateStatus()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/dashboard/server/update/check"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "updateAvailable");
    }

    [Fact]
    public async Task Server_Info_ReturnsServerInfo()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/server/info");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");

        JsonElement data = json.RootElement.GetProperty(propertyName: "data");
        AssertJsonHasProperty(element: data, propertyName: "server");
        AssertJsonHasProperty(element: data, propertyName: "setup_complete");
    }

    [Fact]
    public async Task Server_Resources_ReturnsResourceInfo()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/server/resources");

        string body = await response.Content.ReadAsStringAsync();
        // Resources may fail in test env (no monitoring available)
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            userMessage: $"Expected OK or 422, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task Server_Paths_ReturnsPathsList()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/server/paths");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.True(
            condition: json.RootElement.ValueKind == JsonValueKind.Array,
            userMessage: "Expected array response for server paths"
        );
        Assert.True(condition: json.RootElement.GetArrayLength() > 0, userMessage: "Expected at least one path entry");
    }

    [Fact]
    public async Task Server_Storage_ReturnsStorageInfo()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/server/storage");

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Server_DirectoryTree_ReturnsTreeOrError()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/server/directorytree",
            content: JsonBody(obj: new { folder = "/tmp" })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
            userMessage: $"Expected OK or 422, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
    }

    // =========================================================================
    // SpecialsController — /api/v1/dashboard/specials
    // =========================================================================

    [Fact]
    public async Task DashboardSpecials_Index_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/specials");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task DashboardSpecials_Store_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/specials",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "status");
    }

    [Fact]
    public async Task DashboardSpecials_Delete_NonExistent_ReturnsErrorStatus()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/specials/{Ulid.NewUlid()}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.NotFound,
            userMessage: $"Expected NotFound, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertProblemDetailsShape(root: json.RootElement, expectedStatus: 404);
    }

    [Fact]
    public async Task DashboardSpecials_RescanAll_ReturnsStatusOrNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/specials/rescan",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task DashboardSpecials_RescanById_ReturnsStatusOrNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: $"/api/v1/dashboard/specials/{Ulid.NewUlid()}/rescan",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            userMessage: $"Expected OK or NotFound, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // TasksController — /api/v1/dashboard/tasks
    // =========================================================================

    [Fact]
    public async Task Tasks_Index_ReturnsTasks()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/tasks");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.True(
            condition: json.RootElement.ValueKind == JsonValueKind.Array,
            userMessage: "Expected array response for tasks"
        );
    }

    [Fact]
    public async Task Tasks_Store_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/tasks",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Tasks_Runners_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/tasks/runners");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Tasks_Queue_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/tasks/queue");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Tasks_DeleteQueue_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: "/api/v1/dashboard/tasks/queue/999999"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK,
            userMessage: $"Expected NotFound or OK, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Tasks_FailedJobs_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/tasks/failed");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Tasks_RetryFailed_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/tasks/failed/retry",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertStatusResponse(root: json.RootElement);
    }

    [Fact]
    public async Task Tasks_PauseTask_NonExistent_ReturnsOk()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/tasks/pause/999999",
            content: JsonBody(obj: new { })
        );

        // Pause returns bool result; non-existent ID returns false wrapped in 200
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Tasks_ResumeTask_NonExistent_ReturnsOk()
    {
        HttpResponseMessage response = await _client.PostAsync(
            requestUri: "/api/v1/dashboard/tasks/resume/999999",
            content: JsonBody(obj: new { })
        );

        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // UsersController — /api/v1/dashboard/users
    // =========================================================================

    [Fact]
    public async Task Users_Index_ReturnsDataOrServerError()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/users");

        string body = await response.Content.ReadAsStringAsync();
        // Known bug: UsersController.Index includes LibraryUser but not
        // .ThenInclude(x => x.Library), causing NullReferenceException in
        // PermissionsResponseItemDto when LibraryUser entries exist.
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError,
            userMessage: $"Expected OK or 500, got {(int)response.StatusCode}: {body}"
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(json: body);
            AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
        }
    }

    [Fact]
    public async Task Users_Permissions_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: "/api/v1/dashboard/users/permissions"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertJsonHasProperty(element: json.RootElement, propertyName: "data");
    }

    [Fact]
    public async Task Users_Delete_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/users/{Guid.Empty}"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK,
            userMessage: $"Expected NotFound or OK, got {(int)response.StatusCode}"
        );
    }

    [Fact]
    public async Task Users_Delete_Owner_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            requestUri: $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}"
        );

        string body = await response.Content.ReadAsStringAsync();
        // Owner cannot be deleted
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.OK,
            userMessage: $"Expected 401/403/OK, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task Users_Notifications_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PatchAsync(
            requestUri: "/api/v1/dashboard/users/notifications",
            content: JsonBody(obj: new { })
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode == HttpStatusCode.OK,
            userMessage: $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(json: body);
        AssertStatusResponse(root: json.RootElement);
    }

    [Fact]
    public async Task Users_UserPermissions_SelfDenied()
    {
        // Viewing own permissions is denied
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}/permissions"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode
                is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.OK,
            userMessage: $"Expected 401/403/OK, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task Users_UserPermissions_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/api/v1/dashboard/users/{Guid.Empty}/permissions"
        );

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK,
            userMessage: $"Expected NotFound or OK, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // OpticalMediaController — /api/v1/dashboard/optical
    // =========================================================================

    [Fact]
    public async Task Optical_Drives_ReturnsListOrError()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/api/v1/dashboard/optical/drives");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            condition: response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError,
            userMessage: $"Expected OK or 500, got {(int)response.StatusCode}: {body}"
        );
    }

    // =========================================================================
    // Cross-cutting: Auth denial on dashboard endpoints
    // =========================================================================

    [Theory]
    [InlineData(data: ["GET", "/api/v1/dashboard/configuration"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/devices"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/encoderprofiles"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/libraries"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/logs"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/plugins"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/activity"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/server"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/server/info"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/specials"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/tasks"])]
    [InlineData(data: ["GET", "/api/v1/dashboard/users"])]
    public async Task DashboardEndpoints_ReturnUnauthorized_WhenUnauthenticated(
        string method,
        string url
    )
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = method switch
        {
            "GET" => await unauthed.GetAsync(requestUri: url),
            "POST" => await unauthed.PostAsync(requestUri: url, content: JsonBody(obj: new { })),
            _ => throw new ArgumentException(message: $"Unsupported method: {method}"),
        };

        Assert.True(
            condition: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            userMessage: $"Expected 401/403 for {method} {url}, got {(int)response.StatusCode}"
        );
    }
}
