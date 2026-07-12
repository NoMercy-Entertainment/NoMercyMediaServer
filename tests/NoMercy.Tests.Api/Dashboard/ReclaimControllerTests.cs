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
[Trait("Category", "Reclaim")]
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
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IReclaimScanService>();
                    services.AddSingleton<IReclaimScanService>(fakeService);
                });
            })
            .CreateClient();
    }

    // ── auth ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIndex_Unauthenticated_Returns401()
    {
        HttpClient client = BuildClient(new()).AsUnauthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard/reclaim");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostScan_Unauthenticated_Returns401()
    {
        HttpClient client = BuildClient(new()).AsUnauthenticated();

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/reclaim/scan",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET / before any scan ─────────────────────────────────────────────

    [Fact]
    public async Task GetIndex_BeforeAnyScan_ReturnsIdleWithEmptyItemsAndZeroedSummary()
    {
        HttpClient client = BuildClient(new()).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard/reclaim");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("Idle");
        root.GetProperty("lastScannedAt").ValueKind.Should().Be(JsonValueKind.Null);

        JsonElement summary = root.GetProperty("summary");
        summary.GetProperty("totalReclaimableBytes").GetInt64().Should().Be(0);
        summary.GetProperty("itemCount").GetInt32().Should().Be(0);
        summary.GetProperty("partialJunkCount").GetInt32().Should().Be(0);
        summary.GetProperty("totalPartialJunkBytes").GetInt64().Should().Be(0);

        root.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    // ── POST /scan — 409 when a scan is already running ──────────────────

    [Fact]
    public async Task PostScan_ServiceReportsScanning_Returns409()
    {
        FakeReclaimScanService fakeService = new() { State = ReclaimScanState.Scanning };
        HttpClient client = BuildClient(fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/reclaim/scan",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

        HttpClient client = BuildClient(fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard/reclaim");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("Completed");

        JsonElement summary = root.GetProperty("summary");
        summary.GetProperty("totalReclaimableBytes").GetInt64().Should().Be(123456789L);
        summary.GetProperty("itemCount").GetInt32().Should().Be(1);
        summary.GetProperty("partialJunkCount").GetInt32().Should().Be(0);
        summary.GetProperty("totalPartialJunkBytes").GetInt64().Should().Be(0);

        JsonElement firstItem = root.GetProperty("items")[0];
        firstItem.GetProperty("id").GetString().Should().Be(item.Id);
        firstItem.GetProperty("title").GetString().Should().Be("Test Movie");
        firstItem.GetProperty("mediaType").GetString().Should().Be("movie");
        firstItem.GetProperty("folder").GetString().Should().Be(item.Folder);
        firstItem.GetProperty("servedCopy").GetString().Should().Be("master.m3u8");
        firstItem.GetProperty("kind").GetString().Should().Be("ReclaimableHls");
        firstItem.GetProperty("targetCount").GetInt32().Should().Be(2);
        firstItem.GetProperty("reclaimableBytes").GetInt64().Should().Be(123456789L);

        firstItem
            .TryGetProperty("targetPaths", out _)
            .Should()
            .BeFalse("server-side delete targets must never reach the client");
    }

    // ── DELETE /items/{id} — exception → HTTP status mapping ─────────────

    [Fact]
    public async Task DeleteItem_UnknownId_Returns404()
    {
        FakeReclaimScanService fakeService = new()
        {
            DeleteItemHandler = (_, _) => throw new KeyNotFoundException("not found"),
        };
        HttpClient client = BuildClient(fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.DeleteAsync(
            "/api/v1/dashboard/reclaim/items/unknown-id"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItem_ServedCopyConflict_Returns409()
    {
        FakeReclaimScanService fakeService = new()
        {
            DeleteItemHandler = (_, _) =>
                throw new InvalidOperationException(
                    "Refusing to delete 'x' — it is the currently served copy 'y'."
                ),
        };
        HttpClient client = BuildClient(fakeService).AsAuthenticated();

        HttpResponseMessage response = await client.DeleteAsync(
            "/api/v1/dashboard/reclaim/items/some-id"
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("currently served copy");
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
            DeleteItemHandler?.Invoke(itemId, ct) ?? Task.FromResult(0L);

        public Task<(int count, long bytes)> SweepPartialsAsync(CancellationToken ct) =>
            SweepPartialsHandler?.Invoke(ct) ?? Task.FromResult((0, 0L));
    }
}
