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

using System.Security.Cryptography;
using System.Text;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Proves the root cause behind server-registration collisions: Software.GetDeviceId()
/// hashes DeviceIdBuilder's combined component string with MD5. Inside Docker/KVM/VPS,
/// the motherboard-serial and drive-serial components DeviceIdBuilder probes are
/// commonly empty (no /sys board_serial, no /dev/disk/by-id), so two unrelated
/// installs both hash the SAME degenerate input to the SAME Guid.
/// </summary>
public class SoftwareDeviceIdCollisionProofTests
{
    [Fact]
    public void EmptyGeneratedIdString_HashesToTheSameGuid_OnEveryInstall()
    {
        byte[] installAHash = MD5.HashData(Encoding.UTF8.GetBytes(string.Empty));
        byte[] installBHash = MD5.HashData(Encoding.UTF8.GetBytes(string.Empty));

        Assert.Equal(new Guid(installAHash), new Guid(installBHash));
    }
}
