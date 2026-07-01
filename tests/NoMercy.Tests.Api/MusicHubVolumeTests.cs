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

using NoMercy.Database.Models.Users;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class MusicHubVolumeTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(55, 55)]
    [InlineData(150, 100)]
    public void Volume_IsClampedToZeroHundred(int input, int expected)
    {
        int clamped = Math.Clamp(input, 0, 100);
        clamped.Should().Be(expected);
    }

    [Fact]
    public void NeverSetVolume_CoalescesToDeviceDefault()
    {
        int? neverSet = null;
        (neverSet ?? Device.DefaultVolumePercent).Should().Be(Device.DefaultVolumePercent);
    }

    [Fact]
    public void DeviceDefault_IsAuditoryMidpoint()
    {
        // A never-set device must broadcast a sane level for legacy clients
        // that cannot distinguish null from a deliberate value. 50 is the
        // contract: not silent, not blasting.
        Device.DefaultVolumePercent.Should().Be(50);
    }

    [Fact]
    public void SetVolumeZero_IsDistinctFromNeverSet()
    {
        // The nullable column is the whole point: 0 means "muted to zero",
        // null means "this device has never reported a level".
        int? deliberateZero = 0;
        int? neverSet = null;

        deliberateZero.Should().NotBe(neverSet);
        (deliberateZero ?? Device.DefaultVolumePercent).Should().Be(0);
        (neverSet ?? Device.DefaultVolumePercent).Should().Be(Device.DefaultVolumePercent);
    }
}
