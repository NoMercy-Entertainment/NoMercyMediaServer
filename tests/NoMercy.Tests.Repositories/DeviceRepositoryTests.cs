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

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NoMercy.Api.Hubs;
using NoMercy.Api.WebSockets;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Device")]
public class DeviceRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;
    private static readonly Guid OwnerId = Guid.Parse(input: "aaaabbbb-cccc-dddd-eeee-111111111111");
    private static readonly Guid OtherOwnerId = Guid.Parse(input: "ffffeeee-dddd-cccc-bbbb-222222222222");

    public DeviceRepositoryTests()
    {
        _connection = new(connectionString: "Data Source=:memory:");
        _connection.Open();

        // Foreign keys must be ON so the cascade behaviour configured in
        // MediaContext.OnModelCreating is actually enforced by SQLite.
        using (SqliteCommand fkOn = _connection.CreateCommand())
        {
            fkOn.CommandText = "PRAGMA foreign_keys = ON;";
            fkOn.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(connection: _connection).Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();

        // Seed the two owner User rows required by Device.OwnerUserId FK.
        ctx.Users.AddRange(entities:
            [
                new User
                {
                    Id = OwnerId,
                    Email = "owner@test.local",
                    Name = "Owner",
                },
                new User
                {
                    Id = OtherOwnerId,
                    Email = "other@test.local",
                    Name = "Other Owner",
                }
            ]
        );
        ctx.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext OpenContext()
    {
        return new(options: _options);
    }

    private DeviceRepository BuildRepo(MediaContext ctx)
    {
        return new(context: ctx);
    }

    private static Device MakeDevice(Guid ownerUserId, string suffix = "A")
    {
        return new()
        {
            Id = Ulid.NewUlid(),
            DeviceId = $"device-{suffix}-{Guid.NewGuid()}",
            Name = $"Device {suffix}",
            Type = "phone",
            Browser = "xunit",
            Os = "TestOS",
            Version = "1.0",
            Ip = "127.0.0.1",
            OwnerUserId = ownerUserId,
        };
    }

    // =========================================================================
    // DeleteDeviceWithLogsAsync — device with ActivityLogs does not throw
    // =========================================================================

    [Fact]
    public async Task DeleteDeviceWithLogsAsync_DeviceHasActivityLogs_DeletesWithoutFkThrow()
    {
        Device device = MakeDevice(ownerUserId: OwnerId);

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.Add(entity: device);
        await seedCtx.SaveChangesAsync();

        ActivityLog log = new()
        {
            Category = ActivityCategory.Auth,
            Type = "auth.login",
            Time = DateTime.UtcNow,
            Success = true,
            UserId = OwnerId,
            DeviceId = device.Id,
        };
        seedCtx.ActivityLogs.Add(entity: log);
        await seedCtx.SaveChangesAsync();

        await using MediaContext deleteCtx = OpenContext();
        DeviceRepository repo = BuildRepo(ctx: deleteCtx);

        Func<Task> act = () => repo.DeleteDeviceWithLogsAsync(deviceId: device.Id);
        await act.Should().NotThrowAsync();

        await using MediaContext verifyCtx = OpenContext();
        bool deviceGone = !await verifyCtx.Devices.AnyAsync(predicate: d => d.Id == device.Id);
        bool logsGone = !await verifyCtx.ActivityLogs.AnyAsync(predicate: l => l.DeviceId == device.Id);

        deviceGone.Should().BeTrue(because: "device row must be removed");
        logsGone.Should().BeTrue(because: "activity log rows must be removed before the device");
    }

    // =========================================================================
    // GetOwnerDeviceAsync — returns null for another user's device (404 guard)
    // =========================================================================

    [Fact]
    public async Task GetOwnerDeviceAsync_WrongOwner_ReturnsNull()
    {
        Device device = MakeDevice(ownerUserId: OwnerId);

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.Add(entity: device);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        DeviceRepository repo = BuildRepo(ctx: queryCtx);

        Device? result = await repo.GetOwnerDeviceAsync(deviceId: device.Id, ownerUserId: OtherOwnerId);

        result.Should().BeNull(because: "a different user must not see another owner's device");
    }

    [Fact]
    public async Task GetOwnerDeviceAsync_CorrectOwner_ReturnsDevice()
    {
        Device device = MakeDevice(ownerUserId: OwnerId);

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.Add(entity: device);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        DeviceRepository repo = BuildRepo(ctx: queryCtx);

        Device? result = await repo.GetOwnerDeviceAsync(deviceId: device.Id, ownerUserId: OwnerId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: device.Id);
    }

    // =========================================================================
    // GetOwnerDevicesAsync — only returns devices for that owner
    // =========================================================================

    [Fact]
    public async Task GetOwnerDevicesAsync_ReturnsOnlyDevicesForOwner()
    {
        Device owned1 = MakeDevice(ownerUserId: OwnerId, suffix: "X1");
        Device owned2 = MakeDevice(ownerUserId: OwnerId, suffix: "X2");
        Device other = MakeDevice(ownerUserId: OtherOwnerId, suffix: "Y1");

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.AddRange(entities: [owned1, owned2, other]);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        DeviceRepository repo = BuildRepo(ctx: queryCtx);

        List<Device> result = await repo.GetOwnerDevicesAsync(ownerUserId: OwnerId);

        result.Should().HaveCount(expected: 2);
        result.Should().AllSatisfy(expected: d => d.OwnerUserId.Should().Be(expected: OwnerId));
    }

    // =========================================================================
    // Offline-prune predicate: never removes an online device
    // =========================================================================

    [Fact]
    public async Task OfflinePrune_NeverDeletesOnlineDevice()
    {
        Device onlineDevice = MakeDevice(ownerUserId: OwnerId, suffix: "ONLINE");
        Device offlineDevice = MakeDevice(ownerUserId: OwnerId, suffix: "OFFLINE");

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.AddRange(entities: [onlineDevice, offlineDevice]);
        await seedCtx.SaveChangesAsync();

        Mock<IDbContextFactory<MediaContext>> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: _options));

        Mock<IHubContext<DeviceHub>> hubMock = new();
        Mock<IHubClients> clientsMock = new();
        Mock<IClientProxy> proxyMock = new();
        clientsMock.Setup(expression: c => c.User(It.IsAny<string>())).Returns(value: proxyMock.Object);
        hubMock.Setup(expression: h => h.Clients).Returns(value: clientsMock.Object);

        DeviceBusRegistry registry = new(contextFactory: factoryMock.Object, hubContext: hubMock.Object);

        await registry.Register(deviceId: onlineDevice.Id, ws: null!);

        await using MediaContext queryCtx = OpenContext();
        DeviceRepository repo = BuildRepo(ctx: queryCtx);
        List<Device> allOwned = await repo.GetOwnerDevicesAsync(ownerUserId: OwnerId);

        List<Device> toDelete = allOwned.Where(predicate: d => !registry.IsOnline(deviceId: d.Id)).ToList();

        toDelete.Should().ContainSingle(predicate: d => d.Id == offlineDevice.Id);
        toDelete.Should().NotContain(predicate: d => d.Id == onlineDevice.Id);
    }

    // =========================================================================
    // BroadcastChange fires after delete
    // =========================================================================

    [Fact]
    public async Task BroadcastChange_FiresAfterDeviceDelete()
    {
        Device device = MakeDevice(ownerUserId: OwnerId);

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.Add(entity: device);
        await seedCtx.SaveChangesAsync();

        Mock<IDbContextFactory<MediaContext>> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: _options));

        Mock<IHubContext<DeviceHub>> hubMock = new();
        Mock<IHubClients> clientsMock = new();
        Mock<IClientProxy> proxyMock = new();
        clientsMock.Setup(expression: c => c.User(OwnerId.ToString())).Returns(value: proxyMock.Object);
        hubMock.Setup(expression: h => h.Clients).Returns(value: clientsMock.Object);

        DeviceBusRegistry registry = new(contextFactory: factoryMock.Object, hubContext: hubMock.Object);

        await using MediaContext deleteCtx = OpenContext();
        DeviceRepository repo = BuildRepo(ctx: deleteCtx);
        await repo.DeleteDeviceWithLogsAsync(deviceId: device.Id);

        await registry.BroadcastChange(ownerUserId: OwnerId);

        proxyMock.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "DeviceListChanged",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once,
            failMessage: "BroadcastChange must send DeviceListChanged to the owner"
        );
    }
}
