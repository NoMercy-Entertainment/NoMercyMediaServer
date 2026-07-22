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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Tests.Repositories;

// Covers the DeviceRepository members DeviceRepositoryTests.cs does not touch: plain
// CRUD (GetDevices/AddDeviceAsync/DeleteDeviceAsync/GetByIdAsync/GetAllAsync) and the two
// owner-scoped/global ActivityLog purges.
public class DeviceRepositoryUncoveredTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public DeviceRepositoryUncoveredTests()
    {
        _connection = new(connectionString: "Data Source=:memory:");
        _connection.Open();

        using (SqliteCommand fkOff = _connection.CreateCommand())
        {
            fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
            fkOff.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(connection: _connection).Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext OpenContext()
    {
        return new(options: _options);
    }

    private static Device MakeDevice(Guid? ownerUserId = null, string suffix = "A")
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

    [Fact]
    public async Task GetDevices_IncludesEachDevicesOwnActivityLogsOnly()
    {
        Device deviceWithLog = MakeDevice(suffix: "WithLog");
        Device deviceWithoutLog = MakeDevice(suffix: "WithoutLog");

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.AddRange(entities: [deviceWithLog, deviceWithoutLog]);
        await seedCtx.SaveChangesAsync();

        seedCtx.ActivityLogs.Add(
            entity: new()
            {
                Category = ActivityCategory.Auth,
                Type = "auth.login",
                Time = DateTime.UtcNow,
                UserId = Guid.NewGuid(),
                DeviceId = deviceWithLog.Id,
            }
        );
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        DeviceRepository repository = new(context: queryCtx);

        List<Device> result = await repository.GetDevices();

        result.Should().HaveCount(expected: 2);
        result.Single(predicate: d => d.Id == deviceWithLog.Id).ActivityLogs.Should().ContainSingle();
        result.Single(predicate: d => d.Id == deviceWithoutLog.Id).ActivityLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task AddDeviceAsync_PersistsTheDeviceForLaterRetrieval()
    {
        Device device = MakeDevice(suffix: "Added");

        await using MediaContext ctx = OpenContext();
        DeviceRepository repository = new(context: ctx);

        await repository.AddDeviceAsync(device: device);

        await using MediaContext verifyCtx = OpenContext();
        Device? persisted = await verifyCtx.Devices.FirstOrDefaultAsync(predicate: d => d.Id == device.Id);
        persisted.Should().NotBeNull();
        persisted!.DeviceId.Should().Be(expected: device.DeviceId);
    }

    [Fact]
    public async Task DeleteDeviceAsync_RemovesOnlyTheGivenDevice()
    {
        Device toDelete = MakeDevice(suffix: "ToDelete");
        Device toKeep = MakeDevice(suffix: "ToKeep");

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.AddRange(entities: [toDelete, toKeep]);
        await seedCtx.SaveChangesAsync();

        await using MediaContext deleteCtx = OpenContext();
        Device tracked = await deleteCtx.Devices.FirstAsync(predicate: d => d.Id == toDelete.Id);
        DeviceRepository repository = new(context: deleteCtx);

        await repository.DeleteDeviceAsync(device: tracked);

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.Devices.AnyAsync(predicate: d => d.Id == toDelete.Id)).Should().BeFalse();
        (await verifyCtx.Devices.AnyAsync(predicate: d => d.Id == toKeep.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        DeviceRepository repository = new(context: ctx);

        Device? result = await repository.GetByIdAsync(deviceId: Ulid.NewUlid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_KnownId_ReturnsThatDevice()
    {
        Device device = MakeDevice(suffix: "ById");
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.Add(entity: device);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        DeviceRepository repository = new(context: queryCtx);

        Device? result = await repository.GetByIdAsync(deviceId: device.Id);

        result.Should().NotBeNull();
        result!.DeviceId.Should().Be(expected: device.DeviceId);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEveryDeviceRegardlessOfOwner()
    {
        Guid owner1 = Guid.NewGuid();
        Guid owner2 = Guid.NewGuid();
        Device device1 = MakeDevice(ownerUserId: owner1, suffix: "Owner1");
        Device device2 = MakeDevice(ownerUserId: owner2, suffix: "Owner2");

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.AddRange(entities: [device1, device2]);
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        DeviceRepository repository = new(context: queryCtx);

        List<Device> result = await repository.GetAllAsync();

        result.Should().HaveCount(expected: 2);
        result.Select(selector: d => d.Id).Should().BeEquivalentTo(expectation: [device1.Id, device2.Id]);
    }

    [Fact]
    public async Task DeleteActivityLogsByOwnerAsync_RemovesLogsForDevicesOwnedByThatUser_LeavesOtherOwnersLogs()
    {
        Guid targetOwner = Guid.NewGuid();
        Guid otherOwner = Guid.NewGuid();
        Device ownedDevice = MakeDevice(ownerUserId: targetOwner, suffix: "Owned");
        Device otherDevice = MakeDevice(ownerUserId: otherOwner, suffix: "Other");

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.AddRange(entities: [ownedDevice, otherDevice]);
        await seedCtx.SaveChangesAsync();

        seedCtx.ActivityLogs.AddRange(entities:
            [
                new ActivityLog
                {
                    Category = ActivityCategory.Auth,
                    Type = "auth.login",
                    Time = DateTime.UtcNow,
                    UserId = Guid.NewGuid(),
                    DeviceId = ownedDevice.Id,
                },
                new ActivityLog
                {
                    Category = ActivityCategory.Auth,
                    Type = "auth.login",
                    Time = DateTime.UtcNow,
                    UserId = Guid.NewGuid(),
                    DeviceId = otherDevice.Id,
                }
            ]
        );
        await seedCtx.SaveChangesAsync();

        await using MediaContext deleteCtx = OpenContext();
        DeviceRepository repository = new(context: deleteCtx);

        await repository.DeleteActivityLogsByOwnerAsync(ownerUserId: targetOwner);

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.ActivityLogs.AnyAsync(predicate: l => l.DeviceId == ownedDevice.Id))
            .Should()
            .BeFalse();
        (await verifyCtx.ActivityLogs.AnyAsync(predicate: l => l.DeviceId == otherDevice.Id))
            .Should()
            .BeTrue();
        (await verifyCtx.Devices.AnyAsync(predicate: d => d.Id == ownedDevice.Id))
            .Should()
            .BeTrue(because: "only the log history is cleared, not the device itself");
    }

    [Fact]
    public async Task DeleteAllActivityLogsAsync_RemovesEveryLogAcrossAllDevices()
    {
        Device device1 = MakeDevice(suffix: "All1");
        Device device2 = MakeDevice(suffix: "All2");

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Devices.AddRange(entities: [device1, device2]);
        await seedCtx.SaveChangesAsync();

        seedCtx.ActivityLogs.AddRange(entities:
            [
                new ActivityLog
                {
                    Category = ActivityCategory.Auth,
                    Type = "auth.login",
                    Time = DateTime.UtcNow,
                    UserId = Guid.NewGuid(),
                    DeviceId = device1.Id,
                },
                new ActivityLog
                {
                    Category = ActivityCategory.Playback,
                    Type = "playback.started",
                    Time = DateTime.UtcNow,
                    UserId = Guid.NewGuid(),
                    DeviceId = device2.Id,
                }
            ]
        );
        await seedCtx.SaveChangesAsync();

        await using MediaContext deleteCtx = OpenContext();
        DeviceRepository repository = new(context: deleteCtx);

        await repository.DeleteAllActivityLogsAsync();

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.ActivityLogs.CountAsync()).Should().Be(expected: 0);
        (await verifyCtx.Devices.CountAsync()).Should().Be(expected: 2);
    }
}
