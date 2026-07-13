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

public interface IHardwareCapabilities
{
    IReadOnlyList<GpuDevice> Gpus { get; }
    int CpuCores { get; }
    bool HasGpu { get; }

    /// <summary>
    /// The set of hardware encoder names (ffmpeg encoder handles, e.g.
    /// <c>h264_nvenc</c>) that <see cref="HardwareEncoderProbe"/> confirmed
    /// actually initialize on this host. This is the authority for encoder
    /// SELECTION — a name present in <see cref="IFfmpegCapabilities.AvailableEncoders"/>
    /// but absent here is a compiled-in encoder with no working device behind
    /// it and must never be chosen.
    /// </summary>
    IReadOnlySet<string> UsableHardwareEncoders { get; }

    bool SupportsHardwareEncoding(VideoCodecType codec);
    GpuDevice? GetGpuForCodec(VideoCodecType codec);
}
