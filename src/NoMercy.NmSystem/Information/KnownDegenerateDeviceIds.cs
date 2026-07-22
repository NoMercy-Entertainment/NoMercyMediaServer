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

namespace NoMercy.NmSystem.Information;

/// <summary>
/// Device ids empirically reproduced (DeviceId 6.11.0) from empty/junk DMI data
/// inside Docker/KVM/VPS containers — the motherboard-serial and drive-serial
/// probes DeviceIdBuilder relies on come back empty or provider-wide-shared
/// there, so every affected install's hardware fingerprint hashes to one of
/// these SAME Guids. 75f26c31-97d3-e6d4-eb13-65fbe4bb799e is the confirmed
/// production collision (empty DMI). A server whose resolved identity lands in
/// this set must never keep it — see DeviceIdentityResolver, which migrates it
/// to a fresh unique Guid instead.
/// </summary>
public static class KnownDegenerateDeviceIds
{
    public static readonly IReadOnlySet<Guid> Values = new HashSet<Guid>
    {
        Guid.Parse(input: "75f26c31-97d3-e6d4-eb13-65fbe4bb799e"),
        Guid.Parse(input: "0460d8e2-2a41-eda4-791e-af3284eddac0"),
        Guid.Parse(input: "b32ec49d-e0da-0089-ec7b-06ff222123a2"),
        Guid.Parse(input: "41d49f20-56d5-ce73-6c37-15e49008b842"),
        Guid.Parse(input: "8c380931-2ce8-5c7f-e0a8-1b834c71efff"),
        Guid.Parse(input: "a1cfce7c-7af9-eb46-e304-03fab52bdbbd"),
        Guid.Parse(input: "cf6376ca-b150-cb86-00cf-01d1a7769add"),
        Guid.Parse(input: "55bb248e-f55a-7840-85fa-bcfd8cb088b3"),
    };

    public static bool IsDegenerate(Guid id)
    {
        return Values.Contains(item: id);
    }
}
