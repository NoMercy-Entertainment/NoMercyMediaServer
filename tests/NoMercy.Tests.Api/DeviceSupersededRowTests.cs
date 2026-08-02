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

using NoMercy.Api.WebSockets;
using NoMercy.Database.Models.Users;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// One physical device, offered once.
/// </summary>
/// <remarks>
/// A device that loses its stored id hellos under a value nothing matches and is
/// written a second row. Both carry a fingerprint, so the picker listed one TV
/// twice under one name, and only the newest was ever registered on the bus —
/// choosing the other sent the cast nowhere and read as casting being broken.
/// Observed live: "Tv in woonkamer" and "Bedroom TV" each held two rows.
/// </remarks>
public class DeviceSupersededRowTests
{
    private static Device Row(string name, string type = "tv")
    {
        return new()
        {
            Id = Ulid.NewUlid(),
            Name = name,
            Type = type,
            Fingerprint = Ulid.NewUlid().ToString(),
            IsActive = true,
        };
    }

    [Fact]
    public void ARowNothingIsConnectedToIsSuperseded()
    {
        Device stale = Row("Tv in woonkamer");

        List<Device> superseded = DeviceBusEndpoint.SelectSuperseded([stale], _ => false);

        Assert.Equal([stale], superseded);
    }

    [Fact]
    public void ASecondDeviceThatIsGenuinelyConnectedKeepsItsEntry()
    {
        // Two TVs the owner gave the same name are two devices, not a rotation,
        // and the one on the bus must stay in the picker.
        Device connected = Row("Tv in woonkamer");

        List<Device> superseded = DeviceBusEndpoint.SelectSuperseded(
            [connected],
            id => id == connected.Id
        );

        Assert.Empty(superseded);
    }

    [Fact]
    public void OnlyTheUnreachableRowsAreTakenOut()
    {
        Device connected = Row("Bedroom TV");
        Device stale = Row("Bedroom TV");

        List<Device> superseded = DeviceBusEndpoint.SelectSuperseded(
            [connected, stale],
            id => id == connected.Id
        );

        Assert.Equal([stale], superseded);
    }

    [Fact]
    public void RetiringLeavesEverythingButThePickerEntry()
    {
        // The row is not deleted: the custom name and the stored volume are the
        // owner's settings, and losing them to a reinstall would be its own bug.
        Device stale = Row("Bedroom TV");
        stale.CustomName = "Bedroom TV";
        stale.VolumePercent = 42;

        DeviceBusEndpoint.Retire(stale);

        // GetDevices filters on Fingerprint != null, so this is what removes it.
        Assert.Null(stale.Fingerprint);
        Assert.False(stale.IsActive);
        Assert.Equal("Bedroom TV", stale.CustomName);
        Assert.Equal(42, stale.VolumePercent);
    }
}
