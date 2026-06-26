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
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait("Category", "ServerActivity")]
public class ServerActivityControllerTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    private static readonly Ulid TestDeviceId = Ulid.NewUlid();
    private static readonly Guid TestUserId = TestAuthHandler.DefaultUserId;

    public ServerActivityControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    public async Task InitializeAsync()
    {
        await using MediaContext ctx = new();

        bool deviceExists = await ctx.Devices.AnyAsync(d => d.Id == TestDeviceId);
        if (!deviceExists)
        {
            ctx.Devices.Add(
                new Device
                {
                    Id = TestDeviceId,
                    DeviceId = TestDeviceId.ToString(),
                    Name = "Test Device",
                    Type = "test",
                    Browser = "xunit",
                    Os = "TestOS",
                    Version = "1.0",
                    Ip = "127.0.0.1",
                    OwnerUserId = TestUserId,
                }
            );
            await ctx.SaveChangesAsync();
        }

        await ctx.ActivityLogs.Where(x => x.UserId == TestUserId).ExecuteDeleteAsync();

        List<ActivityLog> rows = [];

        for (int i = 0; i < 5; i++)
        {
            rows.Add(
                new ActivityLog
                {
                    Category = ActivityCategory.Auth,
                    Type = "login",
                    Time = DateTime.UtcNow.AddMinutes(-i),
                    Success = true,
                    UserId = TestUserId,
                    DeviceId = TestDeviceId,
                }
            );
        }

        for (int i = 0; i < 5; i++)
        {
            rows.Add(
                new ActivityLog
                {
                    Category = ActivityCategory.Connection,
                    Type = "connect",
                    Time = DateTime.UtcNow.AddMinutes(-i - 10),
                    Success = i % 2 == 0,
                    UserId = TestUserId,
                    DeviceId = TestDeviceId,
                }
            );
        }

        for (int i = 0; i < 20; i++)
        {
            rows.Add(
                new ActivityLog
                {
                    Category = ActivityCategory.Playback,
                    Type = "play",
                    Time = DateTime.UtcNow.AddMinutes(-i - 20),
                    Success = true,
                    UserId = TestUserId,
                    DeviceId = TestDeviceId,
                }
            );
        }

        ctx.ActivityLogs.AddRange(rows);
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using MediaContext ctx = new();
        await ctx.ActivityLogs.Where(x => x.UserId == TestUserId).ExecuteDeleteAsync();
    }

    // =========================================================================
    // Index — category filter
    // =========================================================================

    [Fact]
    public async Task Index_FilterByCategory_ReturnsOnlyMatchingRows()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            "/api/v1/dashboard/activity?category=Auth"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        JsonElement data = json.RootElement.GetProperty("data");

        Assert.True(data.ValueKind == JsonValueKind.Array, "Expected data to be an array");

        foreach (JsonElement item in data.EnumerateArray())
        {
            string? category = item.GetProperty("category").GetString();
            Assert.Equal("Auth", category);
        }

        Assert.True(
            data.GetArrayLength() == 5,
            $"Expected 5 Auth rows, got {data.GetArrayLength()}"
        );
    }

    // =========================================================================
    // Index — success filter
    // =========================================================================

    [Fact]
    public async Task Index_FilterBySuccessFalse_ReturnsOnlyFailures()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            "/api/v1/dashboard/activity?success=false"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        JsonElement data = json.RootElement.GetProperty("data");

        Assert.True(data.ValueKind == JsonValueKind.Array, "Expected data to be an array");
        Assert.True(data.GetArrayLength() > 0, "Expected at least one failure row");

        foreach (JsonElement item in data.EnumerateArray())
        {
            bool success = item.GetProperty("success").GetBoolean();
            Assert.False(success, "Expected success=false on all returned rows");
        }
    }

    // =========================================================================
    // Index — pagination (Skip + Take)
    // =========================================================================

    [Fact]
    public async Task Index_Pagination_RespectsSkipAndTake()
    {
        HttpResponseMessage fullResponse = await _authed.GetAsync(
            "/api/v1/dashboard/activity?take=100&skip=0"
        );
        string fullBody = await fullResponse.Content.ReadAsStringAsync();
        Assert.True(
            fullResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)fullResponse.StatusCode}: {fullBody}"
        );
        JsonDocument fullJson = JsonDocument.Parse(fullBody);
        JsonElement fullData = fullJson.RootElement.GetProperty("data");
        int totalCount = fullData.GetArrayLength();

        HttpResponseMessage pagedResponse = await _authed.GetAsync(
            "/api/v1/dashboard/activity?take=5&skip=10"
        );
        string pagedBody = await pagedResponse.Content.ReadAsStringAsync();
        Assert.True(
            pagedResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)pagedResponse.StatusCode}: {pagedBody}"
        );

        JsonDocument pagedJson = JsonDocument.Parse(pagedBody);
        JsonElement pagedData = pagedJson.RootElement.GetProperty("data");

        Assert.True(totalCount >= 30, $"Expected at least 30 seeded rows, got {totalCount}");
        Assert.Equal(5, pagedData.GetArrayLength());
    }

    // =========================================================================
    // Index — rejects unauthenticated
    // =========================================================================

    [Fact]
    public async Task Index_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _unauthed.GetAsync("/api/v1/dashboard/activity");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // Destroy — rejects unauthenticated
    // =========================================================================

    [Fact]
    public async Task Destroy_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync("/api/v1/dashboard/activity");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // Destroy — deletes only matching category, leaves others intact
    // =========================================================================

    [Fact]
    public async Task Destroy_ByCategory_DeletesOnlyMatchingRows()
    {
        HttpResponseMessage deleteResponse = await _authed.DeleteAsync(
            "/api/v1/dashboard/activity?category=Auth"
        );

        string deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        Assert.True(
            deleteResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)deleteResponse.StatusCode}: {deleteBody}"
        );

        JsonDocument deleteJson = JsonDocument.Parse(deleteBody);
        JsonElement deleteData = deleteJson.RootElement.GetProperty("data");
        int deleted = deleteData.GetProperty("deleted").GetInt32();
        Assert.True(deleted >= 5, $"Expected at least 5 Auth rows deleted, got {deleted}");

        HttpResponseMessage verifyResponse = await _authed.GetAsync(
            "/api/v1/dashboard/activity?category=Auth&take=100"
        );
        string verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        Assert.True(
            verifyResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK on verify, got {(int)verifyResponse.StatusCode}: {verifyBody}"
        );

        JsonDocument verifyJson = JsonDocument.Parse(verifyBody);
        JsonElement verifyData = verifyJson.RootElement.GetProperty("data");
        Assert.Equal(0, verifyData.GetArrayLength());

        HttpResponseMessage connectionResponse = await _authed.GetAsync(
            "/api/v1/dashboard/activity?category=Connection&take=100"
        );
        string connectionBody = await connectionResponse.Content.ReadAsStringAsync();
        JsonDocument connectionJson = JsonDocument.Parse(connectionBody);
        JsonElement connectionData = connectionJson.RootElement.GetProperty("data");
        Assert.True(
            connectionData.GetArrayLength() >= 5,
            $"Expected Connection rows to survive, got {connectionData.GetArrayLength()}"
        );
    }
}
