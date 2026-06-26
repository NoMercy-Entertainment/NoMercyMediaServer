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

using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Hardware;

public class HardwareCapabilities(IReadOnlyList<GpuDevice> Gpus, int CpuCores)
    : IHardwareCapabilities
{
    public IReadOnlyList<GpuDevice> Gpus { get; } = Gpus;
    public int CpuCores { get; } = CpuCores;
    public bool HasGpu => Gpus.Count > 0;

    public bool SupportsHardwareEncoding(VideoCodecType codec)
    {
        return Gpus.Any(g => g.SupportedCodecs.Contains(codec));
    }

    public GpuDevice? GetGpuForCodec(VideoCodecType codec)
    {
        return Gpus.FirstOrDefault(g => g.SupportedCodecs.Contains(codec));
    }
}
