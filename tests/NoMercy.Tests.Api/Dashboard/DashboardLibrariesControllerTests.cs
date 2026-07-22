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

using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait(name: "Category", value: "DashboardLibraries")]
public class DashboardLibrariesControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    public DashboardLibrariesControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(content: JsonSerializer.Serialize(value: obj), encoding: Encoding.UTF8, mediaType: "application/json");

    private Task<HttpResponseMessage> PatchAsync(HttpClient client, string url, object body) =>
        client.PatchAsync(requestUri: url, content: JsonBody(obj: body));

    private Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body) =>
        client.PostAsync(requestUri: url, content: JsonBody(obj: body));

    [Fact]
    public async Task GetLibraries_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(requestUri: "/api/v1/dashboard/libraries");

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetLibraries_ReturnsOk_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/libraries");

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLibraries_ReturnsEnvelopeWithDataArray()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/libraries");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "dashboard libraries response must have a 'data' property");
        data.ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task GetLibraries_DataItems_ContainRequiredFields()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/libraries");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        data.GetArrayLength().Should().BeGreaterThan(expected: 0, because: "seed data has at least 3 libraries");

        JsonElement item = data.EnumerateArray().First();

        item.TryGetProperty(propertyName: "id", value: out _).Should().BeTrue(because: "dashboard library item must have 'id'");
        item.TryGetProperty(propertyName: "title", value: out _)
            .Should()
            .BeTrue(because: "dashboard library item must have 'title'");
        item.TryGetProperty(propertyName: "type", value: out _)
            .Should()
            .BeTrue(because: "dashboard library item must have 'type'");
        item.TryGetProperty(propertyName: "folder_library", value: out JsonElement folderLibrary)
            .Should()
            .BeTrue(because: "dashboard library item must have 'folder_library'");
        folderLibrary.ValueKind.Should().Be(expected: JsonValueKind.Array);
    }

    [Fact]
    public async Task GetLibraries_DataItems_HavePaginationAndLinkFields()
    {
        HttpResponseMessage response = await _authed.GetAsync(requestUri: "/api/v1/dashboard/libraries");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        JsonElement item = data.EnumerateArray().First();

        item.TryGetProperty(propertyName: "pagination", value: out _)
            .Should()
            .BeTrue(because: "dashboard library item must expose 'pagination' for client routing decisions");
        item.TryGetProperty(propertyName: "link", value: out _)
            .Should()
            .BeTrue(because: "dashboard library item must expose 'link' for client navigation");
    }

    [Fact]
    public async Task PostLibrary_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsync(
            requestUri: "/api/v1/dashboard/libraries",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PostLibrary_ReturnsOkWithStatusAndData_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.PostAsync(requestUri: "/api/v1/dashboard/libraries", content: null);

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status)
            .Should()
            .BeTrue(because: "create-library response must have a 'status' field");
        status.GetString().Should().Be(expected: "ok");

        doc.RootElement.TryGetProperty(propertyName: "data", value: out JsonElement data)
            .Should()
            .BeTrue(because: "create-library response must return the created library in 'data'");
        data.ValueKind.Should().Be(expected: JsonValueKind.Object);
    }

    [Fact]
    public async Task PatchLibrary_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PatchAsync(
            requestUri: $"/api/v1/dashboard/libraries/{NoMercyApiFactory.MovieLibraryId}",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task PatchLibrary_ReturnsNotFound_WhenLibraryDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();
        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: $"/api/v1/dashboard/libraries/{nonExistentId}",
            body: new { title = "Test" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchLibrary_WithAutoEncodeFields_PersistsBothValues()
    {
        Ulid libraryId = await SeedIsolatedLibraryAsync();
        Ulid presetId = await SeedEncodingPresetAsync();

        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: $"/api/v1/dashboard/libraries/{libraryId}",
            body: new { autoEncodeOnScan = true, encodePresetId = presetId.ToString() }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        Library reloaded = await LoadLibraryAsync(libraryId: libraryId);
        reloaded.AutoEncodeOnScan.Should().BeTrue();
        reloaded.EncodePresetId.Should().Be(expected: presetId);
    }

    [Fact]
    public async Task PatchLibrary_OmittingAutoEncodeFields_LeavesExistingValuesUnchanged()
    {
        Ulid presetId = await SeedEncodingPresetAsync();
        Ulid libraryId = await SeedIsolatedLibraryAsync(
            autoEncodeOnScan: true,
            encodePresetId: presetId
        );

        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: $"/api/v1/dashboard/libraries/{libraryId}",
            body: new { title = "Renamed only" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        Library reloaded = await LoadLibraryAsync(libraryId: libraryId);
        reloaded.Title.Should().Be(expected: "Renamed only");
        reloaded.AutoEncodeOnScan.Should().BeTrue();
        reloaded.EncodePresetId.Should().Be(expected: presetId);
    }

    [Fact]
    public async Task PatchLibrary_WithUnknownEncodePresetId_ReturnsNotFound_AndDoesNotChangeLibrary()
    {
        Ulid libraryId = await SeedIsolatedLibraryAsync();
        Ulid unknownPresetId = Ulid.NewUlid();

        HttpResponseMessage response = await PatchAsync(
            client: _authed,
            url: $"/api/v1/dashboard/libraries/{libraryId}",
            body: new { autoEncodeOnScan = true, encodePresetId = unknownPresetId.ToString() }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);

        Library reloaded = await LoadLibraryAsync(libraryId: libraryId);
        reloaded.AutoEncodeOnScan.Should().BeFalse();
        reloaded.EncodePresetId.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLibrary_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            requestUri: $"/api/v1/dashboard/libraries/{NoMercyApiFactory.MovieLibraryId}"
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task DeleteLibrary_ReturnsNotFound_WhenLibraryDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: $"/api/v1/dashboard/libraries/{nonExistentId}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RescanAll_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsync(
            requestUri: "/api/v1/dashboard/libraries/rescan",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task RescanAll_ReturnsOkWithStatusField_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: "/api/v1/dashboard/libraries/rescan",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status)
            .Should()
            .BeTrue(because: "rescan response must include 'status'");
        status.GetString().Should().Be(expected: "ok");
    }

    [Fact]
    public async Task RefreshAll_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.PostAsync(
            requestUri: "/api/v1/dashboard/libraries/refresh",
            content: null
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task RefreshAll_ReturnsOkWithStatusField_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: "/api/v1/dashboard/libraries/refresh",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: body);

        doc.RootElement.TryGetProperty(propertyName: "status", value: out JsonElement status)
            .Should()
            .BeTrue(because: "refresh response must include 'status'");
        status.GetString().Should().Be(expected: "ok");
    }

    [Fact]
    public async Task RescanOne_ReturnsNotFound_WhenLibraryDoesNotExist()
    {
        Ulid nonExistentId = Ulid.NewUlid();
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: $"/api/v1/dashboard/libraries/{nonExistentId}/rescan",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddFolder_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await PostJsonAsync(
            client: _unauthed,
            url: $"/api/v1/dashboard/libraries/{NoMercyApiFactory.MovieLibraryId}/folders",
            body: new { path = "/test", driverId = Ulid.NewUlid().ToString() }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    // Adding a brand-new folder full of pre-existing media must import that
    // media automatically (critical-path #5) — the controller dispatches
    // a LibraryScanJob for the library the folder was just attached to.
    // Every assertion below targets a library seeded fresh inside the test
    // (never one of the shared fixture libraries), so a stray scan dispatched
    // by an unrelated test can't produce a false pass/fail here.
    [Fact]
    public async Task AddFolder_NewFolder_DispatchesLibraryScanJob()
    {
        Ulid libraryId = await SeedIsolatedLibraryAsync();
        string path = $"/media/new-folder-scan-{Ulid.NewUlid()}";

        HttpResponseMessage response = await PostJsonAsync(
            client: _authed,
            url: $"/api/v1/dashboard/libraries/{libraryId}/folders",
            body: new { path, driver_id = Driver.SystemLocalDriverId.ToString() }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        CountDispatchedLibraryScanJobs(libraryId: libraryId)
            .Should()
            .Be(expected: 1, because: "a genuinely new folder must trigger exactly one scan of its library");
    }

    [Fact]
    public async Task AddFolder_PreExistingFolder_DoesNotDispatchLibraryScanJob()
    {
        Ulid firstLibraryId = await SeedIsolatedLibraryAsync();
        Ulid secondLibraryId = await SeedIsolatedLibraryAsync();
        string sharedPath = $"/media/shared-folder-{Ulid.NewUlid()}";

        // Arrange: attach the folder to the first library. It is genuinely
        // new here, so this call is expected to dispatch a scan — that scan
        // is the baseline proving the queue/DB plumbing works, not what this
        // test is asserting on.
        HttpResponseMessage firstResponse = await PostJsonAsync(
            client: _authed,
            url: $"/api/v1/dashboard/libraries/{firstLibraryId}/folders",
            body: new { path = sharedPath, driver_id = Driver.SystemLocalDriverId.ToString() }
        );
        firstResponse.StatusCode.Should().Be(expected: HttpStatusCode.OK);
        CountDispatchedLibraryScanJobs(libraryId: firstLibraryId).Should().Be(expected: 1);

        // Act: attach the SAME driver+path (an already-known folder) to a
        // second, never-touched library. The compat requirement is that
        // re-attaching an existing folder must never trigger a scan.
        HttpResponseMessage secondResponse = await PostJsonAsync(
            client: _authed,
            url: $"/api/v1/dashboard/libraries/{secondLibraryId}/folders",
            body: new { path = sharedPath, driver_id = Driver.SystemLocalDriverId.ToString() }
        );

        secondResponse.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        CountDispatchedLibraryScanJobs(libraryId: secondLibraryId)
            .Should()
            .Be(expected: 0, because: "attaching an already-known folder must never dispatch a new scan");
    }

    private static async Task<Ulid> SeedIsolatedLibraryAsync(
        bool autoEncodeOnScan = false,
        Ulid? encodePresetId = null
    )
    {
        Ulid libraryId = Ulid.NewUlid();

        await using MediaContext mediaContext = new();
        mediaContext.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = $"Isolated Library {libraryId}",
                Type = "movie",
                Order = 99,
                AutoEncodeOnScan = autoEncodeOnScan,
                EncodePresetId = encodePresetId,
            }
        );
        await mediaContext.SaveChangesAsync();

        return libraryId;
    }

    private static async Task<Ulid> SeedEncodingPresetAsync()
    {
        Ulid presetId = Ulid.NewUlid();

        await using MediaContext mediaContext = new();
        mediaContext.EncodingPresets.Add(
            entity: new()
            {
                Id = presetId,
                Name = $"Preset {presetId}",
                ProfileJson = "{}",
                IsBuiltIn = false,
            }
        );
        await mediaContext.SaveChangesAsync();

        return presetId;
    }

    private static async Task<Library> LoadLibraryAsync(Ulid libraryId)
    {
        await using MediaContext mediaContext = new();
        Library? library = await mediaContext.Libraries.FindAsync(keyValues: libraryId);
        library.Should().NotBeNull(because: "the library seeded for this test must still exist");
        return library!;
    }

    private static int CountDispatchedLibraryScanJobs(Ulid libraryId)
    {
        using QueueContext queueContext = new();
        return queueContext
            .QueueJobs.AsEnumerable()
            .Count(predicate: job =>
                job.Payload.Contains(value: "LibraryScanJob") && job.Payload.Contains(value: libraryId.ToString())
            );
    }
}
