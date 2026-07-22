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
[Trait(name: "Category", value: "Unit")]
public sealed class ConnectedClientsAndDeviceListItemTests
{
    [Fact]
    public void ConnectedClients_NewInstance_ClientsIsEmpty()
    {
        ConnectedClients clients = new();

        Assert.Empty(collection: clients.Clients);
    }

    [Fact]
    public void ConnectedClients_TryAdd_StoresUnderConnectionId()
    {
        ConnectedClients clients = new();
        Client client = new() { Endpoint = "/videoHub" };

        bool added = clients.Clients.TryAdd(key: "conn-1", value: client);

        Assert.True(condition: added);
        Assert.Same(expected: client, actual: clients.Clients[key: "conn-1"]);
    }

    [Fact]
    public void ConnectedClients_TryAdd_SameKeyTwice_SecondAddFails()
    {
        ConnectedClients clients = new();
        clients.Clients.TryAdd(key: "conn-1", value: new());

        bool secondAdd = clients.Clients.TryAdd(key: "conn-1", value: new());

        Assert.False(condition: secondAdd);
    }

    [Fact]
    public void ConnectedClients_Remove_DeletesEntry()
    {
        ConnectedClients clients = new();
        clients.Clients.TryAdd(key: "conn-1", value: new());

        bool removed = clients.Clients.Remove(key: "conn-1", value: out _);

        Assert.True(condition: removed);
        Assert.Empty(collection: clients.Clients);
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

        Assert.Equal(expected: deviceId, actual: item.DeviceId);
        Assert.Equal(expected: "fp-abc", actual: item.Fingerprint);
        Assert.Equal(expected: "Bedroom TV", actual: item.Name);
        Assert.Equal(expected: "tv", actual: item.Type);
        Assert.False(condition: item.Online);
        Assert.Null(@object: item.LanIp);
        Assert.Null(value: item.LastSeenAt);
        Assert.False(condition: item.Foreground);
        Assert.False(condition: item.ScreenOn);
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

        Assert.True(condition: foregroundOnly.Foreground);
        Assert.False(condition: foregroundOnly.ScreenOn);
        Assert.False(condition: screenOnOnly.Foreground);
        Assert.True(condition: screenOnOnly.ScreenOn);
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

        Assert.Equal(expected: a, actual: b);
    }
}
