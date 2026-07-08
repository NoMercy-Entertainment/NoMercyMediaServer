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
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait("Category", "Devices")]
public class DevicesControllerTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    private static readonly Guid TestUserId = TestAuthHandler.DefaultUserId;

    // Devices created per-test via InitializeAsync; IDs tracked for cleanup.
    private Ulid _ownedDeviceId;
    private Ulid _otherDeviceId;
    private Ulid _onlineDeviceId;
    private Ulid _offlineDeviceId;

    public DevicesControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    public async Task InitializeAsync()
    {
        await using MediaContext ctx = new();

        _ownedDeviceId = Ulid.NewUlid();
        _otherDeviceId = Ulid.NewUlid();
        _onlineDeviceId = Ulid.NewUlid();
        _offlineDeviceId = Ulid.NewUlid();

        Guid otherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        if (!await ctx.Users.AnyAsync(u => u.Id == otherUserId))
        {
            ctx.Users.Add(
                new()
                {
                    Id = otherUserId,
                    Email = "other@nomercy.tv",
                    Name = "Other User",
                    Owner = false,
                    Allowed = true,
                    Manage = false,
                }
            );
        }

        ctx.Devices.AddRange(
            new Device
            {
                Id = _ownedDeviceId,
                DeviceId = _ownedDeviceId.ToString(),
                Name = "Owned Device",
                Type = "phone",
                Browser = "xunit",
                Os = "TestOS",
                Version = "1.0",
                Ip = "127.0.0.1",
                OwnerUserId = TestUserId,
            },
            new Device
            {
                Id = _otherDeviceId,
                DeviceId = _otherDeviceId.ToString(),
                Name = "Other User Device",
                Type = "phone",
                Browser = "xunit",
                Os = "TestOS",
                Version = "1.0",
                Ip = "127.0.0.2",
                OwnerUserId = otherUserId,
            },
            new Device
            {
                Id = _onlineDeviceId,
                DeviceId = _onlineDeviceId.ToString(),
                Name = "Online Device",
                Type = "tv",
                Browser = "xunit",
                Os = "TestOS",
                Version = "1.0",
                Ip = "127.0.0.3",
                Fingerprint = "online-fp",
                OwnerUserId = TestUserId,
            },
            new Device
            {
                Id = _offlineDeviceId,
                DeviceId = _offlineDeviceId.ToString(),
                Name = "Offline Device",
                Type = "tv",
                Browser = "xunit",
                Os = "TestOS",
                Version = "1.0",
                Ip = "127.0.0.4",
                Fingerprint = "offline-fp",
                OwnerUserId = TestUserId,
            }
        );

        ctx.ActivityLogs.Add(
            new()
            {
                Category = ActivityCategory.Auth,
                Type = "auth.login",
                Time = DateTime.UtcNow,
                Success = true,
                UserId = TestUserId,
                DeviceId = _ownedDeviceId,
            }
        );

        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using MediaContext ctx = new();
        Ulid[] ids = [_ownedDeviceId, _otherDeviceId, _onlineDeviceId, _offlineDeviceId];
        await ctx.Devices.Where(d => ids.Contains(d.Id)).ExecuteDeleteAsync();
    }

    // =========================================================================
    // DELETE /devices/{id} — owner deletes their own device + logs cascade
    // =========================================================================

    [Fact]
    public async Task DestroyOne_OwnDevice_DeletesDeviceAndLogsAndReturns200()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            $"/api/v1/dashboard/devices/{_ownedDeviceId}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        await using MediaContext ctx = new();
        bool deviceGone = !await ctx.Devices.AnyAsync(d => d.Id == _ownedDeviceId);
        bool logsGone = !await ctx.ActivityLogs.AnyAsync(l => l.DeviceId == _ownedDeviceId);

        Assert.True(deviceGone, "Device row must be removed from the DB");
        Assert.True(logsGone, "ActivityLog rows must be cascade-deleted with the device");
    }

    // =========================================================================
    // DELETE /devices/{id} — this is the admin dashboard: a moderator may delete
    // ANY device, including one owned by another user.
    // =========================================================================

    [Fact]
    public async Task DestroyOne_AsModerator_DeletesAnyUsersDevice_Returns200()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            $"/api/v1/dashboard/devices/{_otherDeviceId}"
        );

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 (moderator deletes any device), got {(int)response.StatusCode}: {body}"
        );

        await using MediaContext ctx = new();
        bool deviceGone = !await ctx.Devices.AnyAsync(d => d.Id == _otherDeviceId);
        Assert.True(deviceGone, "A moderator must be able to delete another user's device");
    }

    // =========================================================================
    // DELETE /devices/{id} — 400 on invalid Ulid
    // =========================================================================

    [Fact]
    public async Task DestroyOne_InvalidId_Returns400()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            "/api/v1/dashboard/devices/not-a-valid-ulid"
        );

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 400, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // DELETE /devices/{id} — unauthenticated returns 401/403
    // =========================================================================

    [Fact]
    public async Task DestroyOne_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            $"/api/v1/dashboard/devices/{_ownedDeviceId}"
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // DELETE /devices/offline — prune never removes an online device
    // =========================================================================

    [Fact]
    public async Task DestroyOffline_NeverRemovesOnlineDevice()
    {
        // The controller's offline predicate is:
        //   !busRegistry.IsOnline(d.Id) && !connectedClients.Clients.Values.Any(c => c.Id == d.Id)
        //
        // To reliably exercise the connectedClients branch we spin up a scoped factory
        // via WithWebHostBuilder.  This guarantees ConfigureTestServices runs AFTER all
        // prior service registrations, and the DI container is built with our seeded
        // ConnectedClients instance — which is the exact object the controller receives.
        //
        // Failure proof: if someone removes the !connectedClients... clause from the
        // controller the online device is deleted and the DB assertion goes red.
        // _factory.GetConnectedClients() returns the ConnectedClients singleton that the
        // factory's DI container injects into controllers (SharedConnectedClients property).
        // Seeding it here makes _onlineDeviceId appear "connected" to the controller, so
        // the !connectedClients... guard fires and the device is NOT deleted.
        //
        // Failure proof: if someone removes the !connectedClients... clause from the
        // controller the online device is deleted and the DB assertion goes red.
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        string onlineClientKey = $"test-online-{_onlineDeviceId}";
        connectedClients.Clients[onlineClientKey] = new() { Id = _onlineDeviceId };

        try
        {
            HttpResponseMessage response = await _authed.DeleteAsync(
                "/api/v1/dashboard/devices/offline"
            );

            string body = await response.Content.ReadAsStringAsync();
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected 200, got {(int)response.StatusCode}: {body}"
            );

            JsonDocument json = JsonDocument.Parse(body);
            JsonElement data = json.RootElement.GetProperty("data");

            Assert.True(
                data.TryGetProperty("removed", out _),
                "Response must include 'removed' count"
            );
            Assert.True(
                data.TryGetProperty("devices", out _),
                "Response must include 'devices' list"
            );

            // The online device must NOT be in the removed set — it is still in the DB.
            // Any number of genuinely-offline devices (including _ownedDeviceId and
            // _offlineDeviceId) may have been removed depending on test ordering, but
            // _onlineDeviceId is always protected by the !connectedClients guard.
            await using MediaContext ctx = new();
            bool onlineDeviceStillExists = await ctx.Devices.AnyAsync(d => d.Id == _onlineDeviceId);
            Assert.True(
                onlineDeviceStillExists,
                $"Online device must NOT be deleted — the online guard must have protected it. Body: {body}"
            );

            // Also verify the device is present in the 'devices' remaining list in the response.
            string onlineDeviceIdStr = _onlineDeviceId.ToString().ToLower();
            bool onlineInRemainingList = data.GetProperty("devices")
                .EnumerateArray()
                .Any(d =>
                    d.TryGetProperty("id", out JsonElement idEl)
                    && idEl.GetString()?.ToLower() == onlineDeviceIdStr
                );
            Assert.True(
                onlineInRemainingList,
                $"Online device must appear in the remaining 'devices' list. Body: {body}"
            );
        }
        finally
        {
            // Clean up the seeded client so other tests are not affected.
            connectedClients.Clients.TryRemove(onlineClientKey, out Client? _);
        }
    }

    // =========================================================================
    // DELETE /devices/offline — unauthenticated returns 401/403
    // =========================================================================

    [Fact]
    public async Task DestroyOffline_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync(
            "/api/v1/dashboard/devices/offline"
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // Route disambiguation — literal "offline" must not be parsed as a Ulid id
    // =========================================================================

    [Fact]
    public async Task DestroyOffline_RouteDoesNotCollideWithDestroyOne()
    {
        // "offline" is not a valid Ulid; if route order is wrong this would
        // return 400 (bad id parse) instead of hitting the correct action.
        HttpResponseMessage response = await _authed.DeleteAsync(
            "/api/v1/dashboard/devices/offline"
        );

        // 200 = correct prune action was hit, not the {id} action returning 400.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Route collision: expected 200 from prune action, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // DELETE /devices — clear activity logs for current user's devices
    // =========================================================================

    [Fact]
    public async Task Destroy_ClearsActivityLogsForCurrentUserDevices_Returns200()
    {
        // Insert a fresh activity log for _ownedDeviceId immediately before calling the
        // endpoint.  We do NOT rely on the log seeded in InitializeAsync because parallel
        // test classes (e.g. ServerActivityControllerTests) may delete all TestUserId logs
        // between InitializeAsync and this assertion.
        int freshLogId;
        await using (MediaContext seedCtx = new())
        {
            ActivityLog fresh = new()
            {
                Category = ActivityCategory.Auth,
                Type = "auth.test",
                Time = DateTime.UtcNow,
                Success = true,
                UserId = TestUserId,
                DeviceId = _ownedDeviceId,
            };
            seedCtx.ActivityLogs.Add(fresh);
            await seedCtx.SaveChangesAsync();
            freshLogId = fresh.Id;
        }

        HttpResponseMessage response = await _authed.DeleteAsync("/api/v1/dashboard/devices");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        // The freshly-inserted log must be gone — checked by ID to avoid false positives
        // from parallel tests that may add/remove other logs for the same device.
        await using MediaContext after = new();
        bool freshLogStillExists = await after.ActivityLogs.AnyAsync(l => l.Id == freshLogId);
        Assert.False(
            freshLogStillExists,
            "The freshly-inserted ActivityLog must be deleted by Destroy()"
        );

        // Device rows themselves must be untouched.
        bool deviceStillExists = await after.Devices.AnyAsync(d => d.Id == _ownedDeviceId);
        Assert.True(deviceStillExists, "Device rows must NOT be deleted by Destroy()");
    }

    [Fact]
    public async Task Destroy_AsModerator_ClearsAllActivityLogs_IncludingOtherUsers()
    {
        // Seed a log for the other user's device and capture its id.
        Guid otherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        int otherLogId;
        await using (MediaContext seedCtx = new())
        {
            ActivityLog otherLog = new()
            {
                Category = ActivityCategory.Auth,
                Type = "auth.login",
                Time = DateTime.UtcNow,
                Success = true,
                UserId = otherUserId,
                DeviceId = _otherDeviceId,
            };
            seedCtx.ActivityLogs.Add(otherLog);
            await seedCtx.SaveChangesAsync();
            otherLogId = otherLog.Id;
        }

        HttpResponseMessage response = await _authed.DeleteAsync("/api/v1/dashboard/devices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The admin clear-logs action wipes the entire activity history, so even
        // another user's log is removed. Device rows themselves stay untouched.
        await using MediaContext after = new();
        bool otherLogStillExists = await after.ActivityLogs.AnyAsync(l => l.Id == otherLogId);
        Assert.False(
            otherLogStillExists,
            "Moderator clear-logs must remove ALL activity logs, including other users'"
        );

        bool otherDeviceStillExists = await after.Devices.AnyAsync(d => d.Id == _otherDeviceId);
        Assert.True(otherDeviceStillExists, "Clearing logs must NOT delete device rows");
    }

    [Fact]
    public async Task Destroy_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _unauthed.DeleteAsync("/api/v1/dashboard/devices");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }
}
