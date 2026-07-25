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

public class NullHardwareDetector : IHardwareDetector
{
    public Task<IReadOnlyList<GpuDevice>> DetectGpusAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GpuDevice> empty = [];
        return Task.FromResult(empty);
    }

    public Task<int> DetectCpuCoreCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Environment.ProcessorCount);
    }
}
