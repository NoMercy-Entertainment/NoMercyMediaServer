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

namespace NoMercy.Encoder.Devices;

public record DeviceCapabilities
{
    public int? MaxAudioChannels { get; init; } // 2 = stereo, 6 = 5.1, 8 = 7.1; null = unknown
    public string[] AudioCodecs { get; init; } = []; // canonical lowercase: "aac", "ac3", "eac3", "opus", "flac", "truehd", "dts"; empty = unknown
    public string[] VideoCodecs { get; init; } = []; // "h264", "hevc", "av1", "vp9"; empty = unknown
    public int? MaxVideoHeight { get; init; } // 1080, 1440, 2160; null = unknown
    public bool HdrSupport { get; init; } // false default
    public DolbyVisionProfile DolbyVision { get; init; } = DolbyVisionProfile.None;
    public DeviceRamTier RamTier { get; init; } = DeviceRamTier.Standard;
    public int? PlayerBufferCapMb { get; init; } // null = use server default
    public string? Notes { get; init; } // free-form, debugging only
}

public enum DolbyVisionProfile
{
    None,
    Profile5,
    Profile7,
    Profile81,
    Profile82,
}

public enum DeviceRamTier
{
    LowRam, // <2GB, e.g. cheap Android TV boxes
    Standard, // ~2-4GB, e.g. mid-range TVs / older phones
    HighRam, // 4GB+, e.g. flagship phones, modern TVs
}
