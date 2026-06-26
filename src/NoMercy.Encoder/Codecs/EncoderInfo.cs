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

public record EncoderInfo(
    string FfmpegName,
    GpuVendor? RequiredVendor,
    string[] Presets,
    string[] Profiles,
    string[] Levels,
    QualityRange QualityRange,
    RateControlMode[] SupportedRateControl,
    bool Supports10Bit,
    bool SupportsHdr,
    int MaxConcurrentSessions,
    string PixelFormat10Bit,
    Dictionary<string, string> VendorSpecificFlags
);
