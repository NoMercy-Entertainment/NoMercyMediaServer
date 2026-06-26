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

using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Codecs;

public interface ICodecResolver
{
    ResolvedCodec Resolve(
        VideoCodecType codec,
        IHardwareCapabilities hardware,
        EncoderPreference preference = EncoderPreference.PreferHardware
    );

    /// <summary>
    /// Build a <see cref="ResolvedCodec"/> from a specific FFmpeg encoder name
    /// (e.g. when <see cref="IHardwarePreferenceResolver"/> has already picked
    /// hevc_nvenc from the SpeedIndex). Bypasses the IHardwareCapabilities.HasGpu
    /// gate so a stale / under-detected hardware probe can't override an
    /// encoder that the SpeedIndex has actually measured.
    /// </summary>
    ResolvedCodec ResolveByEncoderName(
        VideoCodecType codec,
        string ffmpegEncoderName,
        IHardwareCapabilities hardware
    );
}
