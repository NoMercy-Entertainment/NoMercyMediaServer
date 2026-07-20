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
[Trait("Category", "Unit")]
public class DeviceDropRuleCronJobTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];

    private IDbContextFactory<MediaContext> CreateFactory()
    {
        SqliteConnection connection = new("DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        _connections.Add(connection);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection)
            .Options;

        using (MediaContext init = new(options))
        {
            init.Database.EnsureCreated();
        }

        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new(options));
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
            seed.Devices.Add(NewDevice(owner, "fp-1", DateTime.UtcNow.AddMinutes(-30)));
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor.OwnerUserId.Should().Be(owner);
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
            seed.Devices.Add(NewDevice(owner, "fp-2", DateTime.UtcNow.AddDays(-8)));
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device dropped = ctx.Devices.Single();
        dropped.OwnerUserId.Should().BeNull();
        dropped.IsActive.Should().BeFalse();
        DeviceDropNotice notice = ctx.DeviceDropNotices.Single();
        notice.UserId.Should().Be(owner);
        notice.Reason.Should().Be("ttl");
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
                NewDevice(owner, "fp-3", DateTime.UtcNow.AddHours(-30), lanIp: "10.0.0.5")
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor.OwnerUserId.Should().Be(owner);
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
                NewDevice(owner, "fp-old", DateTime.UtcNow.AddHours(-30), lanIp: "10.0.0.9")
            );
            // A different fingerprint reclaimed that LAN IP recently via mDNS.
            seed.Devices.Add(
                new Device
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
                    MdnsSeenAt = DateTime.UtcNow.AddHours(-25),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device stale = ctx.Devices.Single(d => d.Fingerprint == "fp-old");
        stale.OwnerUserId.Should().BeNull();
        stale.IsActive.Should().BeFalse();
        DeviceDropNotice notice = ctx.DeviceDropNotices.Single();
        notice.Reason.Should().Be("efuse");
        notice.UserId.Should().Be(owner);
    }

    [Fact]
    public async Task Device_WithNoFingerprintOrOwner_IsNeverACandidate()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "Anonymous",
                    OwnerUserId = null,
                    Fingerprint = null,
                    WsConnectedAt = DateTime.UtcNow.AddDays(-30),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

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
                new Device
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

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor.OwnerUserId.Should().Be(owner);
    }

    [Fact]
    public async Task DroppedDevices_NotifyEachDistinctOwnerExactlyOnce()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        Guid owner = Guid.NewGuid();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(NewDevice(owner, "fp-a", DateTime.UtcNow.AddDays(-10)));
            seed.Devices.Add(NewDevice(owner, "fp-b", DateTime.UtcNow.AddDays(-9)));
            await seed.SaveChangesAsync();
        }

        Mock<IDeviceListChangeNotifier> notifier = new();
        notifier.Setup(n => n.BroadcastChange(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        DeviceDropRuleCronJob job = new(
            factory,
            NullLogger<DeviceDropRuleCronJob>.Instance,
            notifier.Object
        );
        await job.ExecuteAsync(string.Empty);

        notifier.Verify(n => n.BroadcastChange(owner), Times.Once);
    }

    [Fact]
    public async Task NoDevicesDropped_NotifierIsNeverCalled()
    {
        IDbContextFactory<MediaContext> factory = CreateFactory();
        await using (MediaContext seed = await factory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                NewDevice(Guid.NewGuid(), "fp-fresh", DateTime.UtcNow.AddMinutes(-10))
            );
            await seed.SaveChangesAsync();
        }

        Mock<IDeviceListChangeNotifier> notifier = new();

        DeviceDropRuleCronJob job = new(
            factory,
            NullLogger<DeviceDropRuleCronJob>.Instance,
            notifier.Object
        );
        await job.ExecuteAsync(string.Empty);

        notifier.Verify(n => n.BroadcastChange(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UnderlyingFailure_IsLoggedAndRethrown()
    {
        Mock<IDbContextFactory<MediaContext>> throwingFactory = new();
        throwingFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        DeviceDropRuleCronJob job = new(
            throwingFactory.Object,
            NullLogger<DeviceDropRuleCronJob>.Instance
        );

        Func<Task> act = () => job.ExecuteAsync(string.Empty);

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
                new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "mDNS Only",
                    OwnerUserId = owner,
                    Fingerprint = "fp-mdns-only",
                    WsConnectedAt = null,
                    MdnsSeenAt = DateTime.UtcNow.AddDays(-8),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device dropped = ctx.Devices.Single();
        dropped
            .OwnerUserId.Should()
            .BeNull("an 8-day-old mDNS-only sighting must still cross the TTL threshold");
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
                new Device
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    Type = "web",
                    Name = "Both Channels",
                    OwnerUserId = owner,
                    Fingerprint = "fp-both",
                    WsConnectedAt = DateTime.UtcNow.AddDays(-8),
                    MdnsSeenAt = DateTime.UtcNow.AddHours(-1),
                }
            );
            await seed.SaveChangesAsync();
        }

        DeviceDropRuleCronJob job = new(factory, NullLogger<DeviceDropRuleCronJob>.Instance);
        await job.ExecuteAsync(string.Empty);

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        Device survivor = ctx.Devices.Single();
        survivor
            .OwnerUserId.Should()
            .Be(owner, "the more recent mDNS sighting must win over the stale WS timestamp");
    }

    [Fact]
    public void CronExpression_IsHourly()
    {
        DeviceDropRuleCronJob job = new(
            CreateFactory(),
            NullLogger<DeviceDropRuleCronJob>.Instance
        );

        job.CronExpression.Should().Be("0 * * * *");
        job.JobName.Should().Be("Hourly Device Drop-Rule Job");
    }
}
