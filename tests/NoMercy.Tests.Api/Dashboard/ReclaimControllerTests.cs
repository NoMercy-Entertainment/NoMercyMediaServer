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
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.MediaProcessing.Reclaim;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Controller-level tests for the reclaimable-space dashboard endpoints. The
/// delete/scan business logic lives on the real ReclaimScanService and is
/// already covered by the Reclaim tests in NoMercy.Tests.MediaProcessing — these
/// tests only exercise auth, HTTP status mapping and the JSON envelope, driven
/// by a fake <see cref="IReclaimScanService"/> substituted per test via
/// WithWebHostBuilder.
/// </summary>
[Trait(name: "Category", value: "Reclaim")]
public class ReclaimControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public ReclaimControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient BuildClient(FakeReclaimScanService fakeService)
    {
        return _factory
            .WithWebHostBuilder(configuration: builder =>
            {
                builder.ConfigureTestServices(servicesConfiguration: services =>
                {
                    services.RemoveAll<IReclaimScanService>();
                    services.AddSingleton<IReclaimScanService>(implementationInstance: fakeService);
                });
            })
            .CreateClient();
    }

    // ── auth ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIndex_Unauthenticated_Returns401()
    {
        HttpClient client = BuildClient(fakeService: new()).AsUnauthenticated();

        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/dashboard/reclaim");

        response.StatusCode.Should().Be(expected: HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostScan_Unauthenticated_Returns401()
    {
        HttpClient client = BuildClient(fakeService: new()).AsUnauthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/reclaim/scan",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Unauthorized);
    }

    // ── GET / before any scan ─────────────────────────────────────────────

    [Fact]
    public async Task GetIndex_BeforeAnyScan_ReturnsIdleWithEmptyItemsAndZeroedSummary()
    {
        HttpClient client = BuildClient(fakeService: new()).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/dashboard/reclaim");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "status").GetString().Should().Be(expected: "Idle");
        root.GetProperty(propertyName: "lastScannedAt").ValueKind.Should().Be(expected: JsonValueKind.Null);

        JsonElement summary = root.GetProperty(propertyName: "summary");
        summary.GetProperty(propertyName: "totalReclaimableBytes").GetInt64().Should().Be(expected: 0);
        summary.GetProperty(propertyName: "itemCount").GetInt32().Should().Be(expected: 0);
        summary.GetProperty(propertyName: "partialJunkCount").GetInt32().Should().Be(expected: 0);
        summary.GetProperty(propertyName: "totalPartialJunkBytes").GetInt64().Should().Be(expected: 0);

        root.GetProperty(propertyName: "items").GetArrayLength().Should().Be(expected: 0);
    }

    // ── GET / before any scan, while a scan is in progress ────────────────

    [Fact]
    public async Task GetIndex_ScanningBeforeFirstScanCompletes_ReturnsScanningNotIdle()
    {
        FakeReclaimScanService fakeService = new() { State = ReclaimScanState.Scanning };
        HttpClient client = BuildClient(fakeService: fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/dashboard/reclaim");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "status").GetString().Should().Be(expected: "Scanning");

        JsonElement summary = root.GetProperty(propertyName: "summary");
        summary.GetProperty(propertyName: "itemCount").GetInt32().Should().Be(expected: 0);
        root.GetProperty(propertyName: "items").GetArrayLength().Should().Be(expected: 0);
    }

    // ── GET / paging — out-of-range pageIndex never overflows or 500s ────

    [Fact]
    public async Task GetIndex_PageIndexFarBeyondLastPage_ReturnsEmptyItemsNotFirstPage()
    {
        ReclaimableItem item = new(
            Id: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            Title: "Test Movie",
            MediaType: "movie",
            Folder: "/media/movies/Test Movie",
            ServedCopy: "master.m3u8",
            Kind: ReclaimKind.ReclaimableHls,
            TargetPaths: ["/media/movies/Test Movie/720p"],
            ReclaimableBytes: 123456789L
        );

        ReclaimScanResult result = new(
            Items: [item, item, item],
            PartialJunk: [],
            TotalReclaimableBytes: 123456789L,
            TotalPartialJunkBytes: 0L
        );

        FakeReclaimScanService fakeService = new()
        {
            State = ReclaimScanState.Completed,
            LastScannedAt = DateTimeOffset.UtcNow,
            Latest = result,
        };

        HttpClient client = BuildClient(fakeService: fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(
            requestUri: "/api/v1/dashboard/reclaim?pageIndex=5000000&pageSize=500"
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "status").GetString().Should().Be(expected: "Completed");
        root.GetProperty(propertyName: "summary").GetProperty(propertyName: "itemCount").GetInt32().Should().Be(expected: 3);

        JsonElement items = root.GetProperty(propertyName: "items");
        items.ValueKind.Should().Be(expected: JsonValueKind.Array);
        items
            .GetArrayLength()
            .Should()
            .Be(expected: 0, because: "an out-of-range page must be empty, never the first page");
    }

    // ── POST /scan — 409 when a scan is already running ──────────────────

    [Fact]
    public async Task PostScan_ServiceReportsScanning_Returns409()
    {
        FakeReclaimScanService fakeService = new() { State = ReclaimScanState.Scanning };
        HttpClient client = BuildClient(fakeService: fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            requestUri: "/api/v1/dashboard/reclaim/scan",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Conflict);
    }

    // ── GET / envelope shape (camelCase, no leaked target paths) ─────────

    [Fact]
    public async Task GetIndex_WithItems_ReturnsCamelCaseItemEnvelopeWithoutTargetPaths()
    {
        ReclaimableItem item = new(
            Id: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            Title: "Test Movie",
            MediaType: "movie",
            Folder: "/media/movies/Test Movie",
            ServedCopy: "master.m3u8",
            Kind: ReclaimKind.ReclaimableHls,
            TargetPaths: ["/media/movies/Test Movie/720p", "/media/movies/Test Movie/480p"],
            ReclaimableBytes: 123456789L
        );

        ReclaimScanResult result = new(
            Items: [item],
            PartialJunk: [],
            TotalReclaimableBytes: 123456789L,
            TotalPartialJunkBytes: 0L
        );

        FakeReclaimScanService fakeService = new()
        {
            State = ReclaimScanState.Completed,
            LastScannedAt = DateTimeOffset.UtcNow,
            Latest = result,
        };

        HttpClient client = BuildClient(fakeService: fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/dashboard/reclaim");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement root = doc.RootElement;

        root.GetProperty(propertyName: "status").GetString().Should().Be(expected: "Completed");

        JsonElement summary = root.GetProperty(propertyName: "summary");
        summary.GetProperty(propertyName: "totalReclaimableBytes").GetInt64().Should().Be(expected: 123456789L);
        summary.GetProperty(propertyName: "itemCount").GetInt32().Should().Be(expected: 1);
        summary.GetProperty(propertyName: "partialJunkCount").GetInt32().Should().Be(expected: 0);
        summary.GetProperty(propertyName: "totalPartialJunkBytes").GetInt64().Should().Be(expected: 0);

        JsonElement firstItem = root.GetProperty(propertyName: "items")[index: 0];
        firstItem.GetProperty(propertyName: "id").GetString().Should().Be(expected: item.Id);
        firstItem.GetProperty(propertyName: "title").GetString().Should().Be(expected: "Test Movie");
        firstItem.GetProperty(propertyName: "mediaType").GetString().Should().Be(expected: "movie");
        firstItem.GetProperty(propertyName: "folder").GetString().Should().Be(expected: item.Folder);
        firstItem.GetProperty(propertyName: "servedCopy").GetString().Should().Be(expected: "master.m3u8");
        firstItem.GetProperty(propertyName: "kind").GetString().Should().Be(expected: "ReclaimableHls");
        firstItem.GetProperty(propertyName: "targetCount").GetInt32().Should().Be(expected: 2);
        firstItem.GetProperty(propertyName: "reclaimableBytes").GetInt64().Should().Be(expected: 123456789L);

        firstItem
            .TryGetProperty(propertyName: "targetPaths", value: out _)
            .Should()
            .BeFalse(because: "server-side delete targets must never reach the client");
    }

    // ── DELETE /items/{id} — exception → HTTP status mapping ─────────────

    [Fact]
    public async Task DeleteItem_UnknownId_Returns404()
    {
        FakeReclaimScanService fakeService = new()
        {
            DeleteItemHandler = (_, _) => throw new KeyNotFoundException(message: "not found"),
        };
        HttpClient client = BuildClient(fakeService: fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.DeleteAsync(
            requestUri: "/api/v1/dashboard/reclaim/items/unknown-id"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItem_ServedCopyConflict_Returns409()
    {
        FakeReclaimScanService fakeService = new()
        {
            DeleteItemHandler = (_, _) =>
                throw new InvalidOperationException(
                    message: "Refusing to delete 'x' — it is the currently served copy 'y'."
                ),
        };
        HttpClient client = BuildClient(fakeService: fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.DeleteAsync(
            requestUri: "/api/v1/dashboard/reclaim/items/some-id"
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.Conflict, because: body);
        body.Should().Contain(expected: "currently served copy");
    }

    // ── fake service ───────────────────────────────────────────────────────

    private sealed class FakeReclaimScanService : IReclaimScanService
    {
        public ReclaimScanState State { get; set; } = ReclaimScanState.Idle;

        public DateTimeOffset? LastScannedAt { get; set; }

        public ReclaimScanResult? Latest { get; set; }

        public Func<string, CancellationToken, Task<long>>? DeleteItemHandler { get; set; }

        public Func<
            CancellationToken,
            Task<(int count, long bytes)>
        >? SweepPartialsHandler { get; set; }

        public Task StartScanAsync(CancellationToken ct)
        {
            State = ReclaimScanState.Scanning;
            return Task.CompletedTask;
        }

        public Task<long> DeleteItemAsync(string itemId, CancellationToken ct) =>
            DeleteItemHandler?.Invoke(arg1: itemId, arg2: ct) ?? Task.FromResult(result: 0L);

        public Task<(int count, long bytes)> SweepPartialsAsync(CancellationToken ct) =>
            SweepPartialsHandler?.Invoke(arg: ct) ?? Task.FromResult(result: (0, 0L));
    }
}
