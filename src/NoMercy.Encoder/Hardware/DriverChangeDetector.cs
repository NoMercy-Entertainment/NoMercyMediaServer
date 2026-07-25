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

namespace NoMercy.Encoder.Hardware;

public class DriverChangeDetector(IHardwareDetector hardwareDetector, IDriverFingerprintStore store)
    : IDriverChangeDetector
{
    public async Task<DriverChangeResult> DetectAndPersistAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GpuDevice> gpus = await hardwareDetector.DetectGpusAsync(ct);

        List<GpuDriverInfo> driverInfos = gpus.Select(
                (gpu, index) =>
                    new GpuDriverInfo(
                        gpu.Vendor.ToString(),
                        gpu.Name,
                        gpu.DriverVersion ?? string.Empty,
                        index
                    )
            )
            .ToList();

        DriverFingerprint fingerprint = new(driverInfos);
        string currentHash = fingerprint.ComputeHash();

        string? previousHash = await store.LoadHashAsync(ct);
        await store.SaveHashAsync(currentHash, ct);

        bool isFirstBoot = previousHash is null;
        bool changed = !isFirstBoot && previousHash != currentHash;

        return new(
            currentHash,
            previousHash,
            changed,
            isFirstBoot
        );
    }
}
