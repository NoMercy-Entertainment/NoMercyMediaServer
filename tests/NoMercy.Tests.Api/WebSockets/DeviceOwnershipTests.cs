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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.WebSockets;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using Xunit;

namespace NoMercy.Tests.Api.WebSockets;

/// <summary>
/// Cross-account isolation for the device bus: a device's ownership follows the
/// account it is authenticated as, so it can never stay attached to (or surface
/// on) the account that paired it first once it logs into another account.
/// </summary>
public class DeviceOwnershipTests
{
    private static MediaContext MakeContext()
    {
        // SQLite in-memory without "Foreign Keys=True" so we can seed devices with
        // arbitrary owner ids without materializing full User rows.
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();
        // Devices reference Users via a FK; these unit tests exercise ownership
        // routing in isolation, so disable FK enforcement instead of materializing
        // full User rows.
        using (SqliteCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection)
            .Options;
        MediaContext context = new(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task NewDevice_isOwnedByConnectingUser()
    {
        await using MediaContext ctx = MakeContext();
        Guid userA = Guid.NewGuid();

        (Device device, Guid? previousOwner) =
            await DeviceBusEndpoint.ResolveOwnedDeviceAsync(ctx, "fp-1", userA, "TV", "tv");

        previousOwner.Should().BeNull();
        device.OwnerUserId.Should().Be(userA);
    }

    [Fact]
    public async Task Device_transfersToNewAccount_andReportsPreviousOwner()
    {
        await using MediaContext ctx = MakeContext();
        Guid userA = Guid.NewGuid();
        Guid userB = Guid.NewGuid();
        ctx.Devices.Add(new()
        {
            DeviceId = "fp-1",
            Fingerprint = "fp-1",
            OwnerUserId = userB,
            Name = "TV",
            Type = "tv",
        });
        await ctx.SaveChangesAsync();

        (Device device, Guid? previousOwner) =
            await DeviceBusEndpoint.ResolveOwnedDeviceAsync(ctx, "fp-1", userA, "TV", "tv");

        // ownership moved to the account the device is now logged into
        device.OwnerUserId.Should().Be(userA);
        previousOwner.Should().Be(userB);
        // the existing row was re-owned, not duplicated (DeviceId is unique)
        (await ctx.Devices.CountAsync(d => d.DeviceId == "fp-1")).Should().Be(1);
    }

    [Fact]
    public async Task Reconnect_sameOwner_isNotATransfer()
    {
        await using MediaContext ctx = MakeContext();
        Guid userA = Guid.NewGuid();
        ctx.Devices.Add(new()
        {
            DeviceId = "fp-1",
            Fingerprint = "fp-1",
            OwnerUserId = userA,
            Name = "TV",
            Type = "tv",
        });
        await ctx.SaveChangesAsync();

        (Device device, Guid? previousOwner) =
            await DeviceBusEndpoint.ResolveOwnedDeviceAsync(ctx, "fp-1", userA, "TV", "tv");

        device.OwnerUserId.Should().Be(userA);
        // previousOwner == caller, so HandleHello skips the previous-owner refresh
        previousOwner.Should().Be(userA);
    }
}
