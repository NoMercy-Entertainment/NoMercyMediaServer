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

using NoMercy.Networking.Devices;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: ConnectedClients is the process-wide connection registry every
/// hub reads/writes by connection id — it must start empty and behave as a
/// plain thread-safe map (add, overwrite-on-same-key, remove). DeviceListItem
/// must carry every field the device-switcher UI depends on (identity,
/// presence, and the TV-side foreground/screen-on flags).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConnectedClientsAndDeviceListItemTests
{
    [Fact]
    public void ConnectedClients_NewInstance_ClientsIsEmpty()
    {
        ConnectedClients clients = new();

        Assert.Empty(clients.Clients);
    }

    [Fact]
    public void ConnectedClients_TryAdd_StoresUnderConnectionId()
    {
        ConnectedClients clients = new();
        Client client = new() { Endpoint = "/videoHub" };

        bool added = clients.Clients.TryAdd("conn-1", client);

        Assert.True(added);
        Assert.Same(client, clients.Clients["conn-1"]);
    }

    [Fact]
    public void ConnectedClients_TryAdd_SameKeyTwice_SecondAddFails()
    {
        ConnectedClients clients = new();
        clients.Clients.TryAdd("conn-1", new());

        bool secondAdd = clients.Clients.TryAdd("conn-1", new());

        Assert.False(secondAdd);
    }

    [Fact]
    public void ConnectedClients_Remove_DeletesEntry()
    {
        ConnectedClients clients = new();
        clients.Clients.TryAdd("conn-1", new());

        bool removed = clients.Clients.Remove("conn-1", out _);

        Assert.True(removed);
        Assert.Empty(clients.Clients);
    }

    [Fact]
    public void DeviceListItem_RequiredFieldsRoundTrip()
    {
        Ulid deviceId = Ulid.NewUlid();

        DeviceListItem item = new()
        {
            DeviceId = deviceId,
            Fingerprint = "fp-abc",
            Name = "Bedroom TV",
            Type = "tv",
        };

        Assert.Equal(deviceId, item.DeviceId);
        Assert.Equal("fp-abc", item.Fingerprint);
        Assert.Equal("Bedroom TV", item.Name);
        Assert.Equal("tv", item.Type);
        Assert.False(item.Online);
        Assert.Null(item.LanIp);
        Assert.Null(item.LastSeenAt);
        Assert.False(item.Foreground);
        Assert.False(item.ScreenOn);
        Assert.False(item.CastReachable);
    }

    [Fact]
    public void DeviceListItem_ForegroundAndScreenOn_AreIndependentFlags()
    {
        DeviceListItem foregroundOnly = new()
        {
            DeviceId = Ulid.NewUlid(),
            Fingerprint = "fp-1",
            Name = "TV",
            Type = "tv",
            Foreground = true,
            ScreenOn = false,
        };
        DeviceListItem screenOnOnly = new()
        {
            DeviceId = Ulid.NewUlid(),
            Fingerprint = "fp-2",
            Name = "TV",
            Type = "tv",
            Foreground = false,
            ScreenOn = true,
        };

        Assert.True(foregroundOnly.Foreground);
        Assert.False(foregroundOnly.ScreenOn);
        Assert.False(screenOnOnly.Foreground);
        Assert.True(screenOnOnly.ScreenOn);
    }

    [Fact]
    public void DeviceListItem_RecordEquality_SameValues_AreEqual()
    {
        Ulid deviceId = Ulid.NewUlid();
        DateTime seenAt = DateTime.UtcNow;

        DeviceListItem a = new()
        {
            DeviceId = deviceId,
            Fingerprint = "fp",
            Name = "TV",
            Type = "tv",
            Online = true,
            LanIp = "192.168.1.5",
            LastSeenAt = seenAt,
        };
        DeviceListItem b = new()
        {
            DeviceId = deviceId,
            Fingerprint = "fp",
            Name = "TV",
            Type = "tv",
            Online = true,
            LanIp = "192.168.1.5",
            LastSeenAt = seenAt,
        };

        Assert.Equal(a, b);
    }
}
