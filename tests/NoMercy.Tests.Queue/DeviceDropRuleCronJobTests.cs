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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Devices;
using NoMercy.Queue.MediaServer.Jobs;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="DeviceDropRuleCronJob"/> is the hourly decision that disowns
/// stale device-registry rows so a picker stops showing a duplicate/dead
/// entry. Every branch here is a real user-facing outcome, not bookkeeping:
/// a device seen recently must survive untouched; one past the 7-day TTL
/// must be dropped outright; one in the 1h-24h window is only dropped if its
/// LAN-IP slot was demonstrably reclaimed by a different device fingerprint
/// (the "e-fuse" case — otherwise a laptop that sleeps overnight would get
/// disowned for no reason). If any of these thresholds regress, a real
/// device silently vanishes from (or lingers in) a user's picker.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class DeviceDropRuleCronJobTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];

    private IDbContextFactory<MediaContext> CreateFactory()
    {
        SqliteConnection connection = new(connectionString: "DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        _connections.Add(item: connection);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: connection)
            .Options;

        using (MediaContext init = new(options: options))
        {
            init.Database.EnsureCreated();
        }

        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(expression: x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: options));
        return mock.Object;
    }

    public void Dispose()
    {
        foreach (SqliteConnection connection in _connections)
            connection.Dispose();
    }

    private static Device NewDevice(
        Guid ownerId,
        string fingerprint,
        DateTime? wsConnectedAt,
        string? lanIp = null
    ) =>
        new()
        {
            DeviceId = Guid.NewGuid().ToString(),
            Type = "web",
            Name = "Test Device",
            OwnerUserId = ownerId,
            Fingerprint = fingerprint,
            WsConnectedAt = wsConnectedAt,
            LanIp = lanIp,
        };

    [Fact]
    public async Task Device_SeenWithinGraceWindow_IsNotDropped()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(entity: NewDevice(ownerId: owner, fingerprint: "fp-1", wsConnectedAt: DateTime.UtcNow.AddMinutes(value: -30)));
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor.OwnerUserId.Should().Be(expected: owner);
        survivor.IsActive.Should().BeFalse(); // never set true by the job; unrelated to drop
        ctx.DeviceDropNotices.Should().BeEmpty();
    }

    [Fact]
    public async Task Device_BeyondTtlWindow_IsDropped_WithTtlReasonNotice()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(entity: NewDevice(ownerId: owner, fingerprint: "fp-2", wsConnectedAt: DateTime.UtcNow.AddDays(value: -8)));
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device dropped = ctx.Devices.Single();
        dropped.OwnerUserId.Should().BeNull();
        dropped.IsActive.Should().BeFalse();
        DeviceDropNotice notice = ctx.DeviceDropNotices.Single();
        notice.UserId.Should().Be(expected: owner);
        notice.Reason.Should().Be(expected: "ttl");
    }

    [Fact]
    public async Task Device_InEfuseWindow_WithNoSlotReclaim_IsNotDropped()
    {
        // Past the 24h e-fuse threshold (and under the 7d TTL) reaches the
        // slot-reclaim check, but with no competing device on the same LAN
        // IP the job must leave it alone — a laptop asleep for a day is not
        // "reclaimed" just because time passed.
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: NewDevice(ownerId: owner, fingerprint: "fp-3", wsConnectedAt: DateTime.UtcNow.AddHours(value: -30), lanIp: "10.0.0.5")
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor.OwnerUserId.Should().Be(expected: owner);
        ctx.DeviceDropNotices.Should().BeEmpty();
    }

    [Fact]
    public async Task Device_InEfuseWindow_WithSlotReclaimedByDifferentFingerprint_IsDropped()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            // The stale device — same LanIp, hasn't been seen in the efuse window.
            seed.Devices.Add(
                entity: NewDevice(ownerId: owner, fingerprint: "fp-old", wsConnectedAt: DateTime.UtcNow.AddHours(value: -30), lanIp: "10.0.0.9")
            );
            // A different fingerprint reclaimed that LAN IP recently via mDNS.
            seed.Devices.Add(
                entity: new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "New Occupant",
                    OwnerUserId = null,
                    Fingerprint = "fp-new",
                    LanIp = "10.0.0.9",
                    // The reclaim check compares MdnsSeenAt <= (now - EFuseWindow):
                    // the occupant must have held this LAN IP since at least the
                    // e-fuse threshold, not merely "recently", so a one-off DHCP
                    // blip can't falsely disown the original device.
                    MdnsSeenAt = DateTime.UtcNow.AddHours(value: -25),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device stale = ctx.Devices.Single(predicate: d => d.Fingerprint == "fp-old");
        stale.OwnerUserId.Should().BeNull();
        stale.IsActive.Should().BeFalse();
        DeviceDropNotice notice = ctx.DeviceDropNotices.Single();
        notice.Reason.Should().Be(expected: "efuse");
        notice.UserId.Should().Be(expected: owner);
    }

    [Fact]
    public async Task Device_WithNoFingerprintOrOwner_IsNeverACandidate()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "Anonymous",
                    OwnerUserId = null,
                    Fingerprint = null,
                    WsConnectedAt = DateTime.UtcNow.AddDays(value: -30),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.Devices.Should().ContainSingle();
        ctx.DeviceDropNotices.Should().BeEmpty();
    }

    [Fact]
    public async Task Device_NeverSeen_HasNullLastSeen_IsSkippedNotDropped()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "Registered Not Seen",
                    OwnerUserId = owner,
                    Fingerprint = "fp-neverseen",
                    WsConnectedAt = null,
                    MdnsSeenAt = null,
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor.OwnerUserId.Should().Be(expected: owner);
    }

    [Fact]
    public async Task DroppedDevices_NotifyEachDistinctOwnerExactlyOnce()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(entity: NewDevice(ownerId: owner, fingerprint: "fp-a", wsConnectedAt: DateTime.UtcNow.AddDays(value: -10)));
            seed.Devices.Add(entity: NewDevice(ownerId: owner, fingerprint: "fp-b", wsConnectedAt: DateTime.UtcNow.AddDays(value: -9)));
            await seed.SaveChangesAsync();
        }

        Mock<IDeviceListChangeNotifier> notifier = new();
        notifier.Setup(expression: n => n.BroadcastChange(It.IsAny<Guid>())).Returns(value: Task.CompletedTask);

        DeviceDropRuleCronJob job = new(
            contextFactory: factory,
            logger: NullLogger<DeviceDropRuleCronJob>.Instance,
            changeNotifier: notifier.Object
        );
        await job.ExecuteAsync(parameters: string.Empty);

        notifier.Verify(expression: n => n.BroadcastChange(owner), times: Times.Once);
    }

    [Fact]
    public async Task NoDevicesDropped_NotifierIsNeverCalled()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: NewDevice(ownerId: Guid.NewGuid(), fingerprint: "fp-fresh", wsConnectedAt: DateTime.UtcNow.AddMinutes(value: -10))
            );
            await seed.SaveChangesAsync();
        }

        Mock<IDeviceListChangeNotifier> notifier = new();

        DeviceDropRuleCronJob job = new(
            contextFactory: factory,
            logger: NullLogger<DeviceDropRuleCronJob>.Instance,
            changeNotifier: notifier.Object
        );
        await job.ExecuteAsync(parameters: string.Empty);

        notifier.Verify(expression: n => n.BroadcastChange(It.IsAny<Guid>()), times: Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UnderlyingFailure_IsLoggedAndRethrown()
    {
        Mock<IDbContextFactory<MediaContext>> throwingFactory = new();
        throwingFactory
            .Setup(expression: f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception: new InvalidOperationException(message: "db unavailable"));

        DeviceDropRuleCronJob job = new(
            contextFactory: throwingFactory.Object,
            logger: NullLogger<DeviceDropRuleCronJob>.Instance
        );

        Func<Task> act = () => job.ExecuteAsync(parameters: string.Empty);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Device_SeenOnlyViaMdns_NoWsConnection_UsesMdnsAsLastSeen()
    {
        // MaxOf(WsConnectedAt, MdnsSeenAt): WsConnectedAt is null here, so the
        // decision must fall through to the mDNS timestamp rather than
        // treating the device as "never seen".
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "mDNS Only",
                    OwnerUserId = owner,
                    Fingerprint = "fp-mdns-only",
                    WsConnectedAt = null,
                    MdnsSeenAt = DateTime.UtcNow.AddDays(value: -8),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device dropped = ctx.Devices.Single();
        dropped
            .OwnerUserId.Should()
            .BeNull(because: "an 8-day-old mDNS-only sighting must still cross the TTL threshold");
    }

    [Fact]
    public async Task Device_SeenViaBothChannels_UsesTheMoreRecentTimestamp()
    {
        // MaxOf's final branch: both timestamps present, take the later one.
        // WsConnectedAt (8 days ago) alone would cross the TTL; MdnsSeenAt (1
        // hour ago) is the true, more recent last-seen and must win, keeping
        // the device inside the grace window.
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "Both Channels",
                    OwnerUserId = owner,
                    Fingerprint = "fp-both",
                    WsConnectedAt = DateTime.UtcNow.AddDays(value: -8),
                    MdnsSeenAt = DateTime.UtcNow.AddHours(value: -1),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(contextFactory: factory, logger: NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(parameters: string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor
            .OwnerUserId.Should()
            .Be(expected: owner, because: "the more recent mDNS sighting must win over the stale WS timestamp");
    }

    [Fact]
    public void CronExpression_IsHourly()
    {
        DeviceDropRuleCronJob job = new(
            contextFactory: CreateFactory(),
            logger: NullLogger<DeviceDropRuleCronJob>.Instance
        );

        job.CronExpression.Should().Be(expected: "0 * * * *");
        job.JobName.Should().Be(expected: "Hourly Device Drop-Rule Job");
    }
}
