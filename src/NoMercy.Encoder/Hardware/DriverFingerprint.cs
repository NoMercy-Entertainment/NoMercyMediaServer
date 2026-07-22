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

namespace NoMercy.Encoder.Hardware;

public record GpuDriverInfo(string Vendor, string Model, string DriverVersion, int Index);

public record DriverFingerprint(IReadOnlyList<GpuDriverInfo> Gpus)
{
    /// <summary>
    /// Computes a stable lowercase hex SHA-256 over the GPU list.
    /// Entries are sorted ordinally before hashing so insertion-order changes
    /// do not produce a false positive (e.g. driver enumerating GPUs in a
    /// different order after a reboot).
    /// </summary>
    public string ComputeHash()
    {
        IEnumerable<string> parts = Gpus.Select(selector: g => $"{g.Vendor}|{g.Model}|{g.DriverVersion}")
            .OrderBy(keySelector: s => s, comparer: StringComparer.Ordinal);

        string payload = string.Join(separator: ";", values: parts);
        byte[] hashBytes = SHA256.HashData(source: Encoding.UTF8.GetBytes(s: payload));
        return Convert.ToHexStringLower(inArray: hashBytes);
    }
}
