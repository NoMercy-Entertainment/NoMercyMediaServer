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

namespace NoMercy.Encoder.Codecs.Definitions;

public class Vp9Definition : ICodecDefinition
{
    public VideoCodecType CodecType => VideoCodecType.Vp9;

    public EncoderInfo[] Encoders =>
        [
            // Software encoder — libvpx-vp9
            // No presets. 4 profiles: profile0 (8-bit 4:2:0), profile1 (8-bit 4:2:2/4:4:4),
            // profile2 (10/12-bit 4:2:0), profile3 (10/12-bit 4:2:2/4:4:4).
            // CRF 0-63. 10-bit via profile2/profile3. Unlimited sessions.
            // NOTE: vp9_nvenc, vp9_amf, vp9_videotoolbox do NOT exist.
            // VP9 hardware encoding is Intel-only (QSV + VAAPI).
            new(
                FfmpegName: "libvpx-vp9",
                RequiredVendor: null,
                Presets: [],
                Profiles: ["profile0", "profile1", "profile2", "profile3"],
                Levels: [],
                QualityRange: new(Min: 0, Max: 63, Default: 33),
                SupportedRateControl:
                [
                    RateControlMode.Crf,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: true,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
            ),
            // Intel Quick Sync Video — vp9_qsv
            // 7 presets (veryfast→veryslow). Quality range 1-51 (NOT 0). Unlimited sessions.
            // Intel-only — no NVIDIA or AMD VP9 hardware encoder exists.
            new(
                FfmpegName: "vp9_qsv",
                RequiredVendor: GpuVendor.Intel,
                Presets: ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                Profiles: [],
                Levels: [],
                QualityRange: new(Min: 1, Max: 51, Default: 33),
                SupportedRateControl:
                [
                    RateControlMode.Icq,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "",
                VendorSpecificFlags: new()
            ),
            // Intel VAAPI — vp9_vaapi
            // No presets. 4 profiles: profile0-3 (mirrors libvpx-vp9 profile numbering).
            // QP 0-255 (VA-API full range). Linux VA-API path. Unlimited sessions.
            new(
                FfmpegName: "vp9_vaapi",
                RequiredVendor: GpuVendor.Intel,
                Presets: [],
                Profiles: ["profile0", "profile1", "profile2", "profile3"],
                Levels: [],
                QualityRange: new(Min: 0, Max: 255, Default: 100),
                SupportedRateControl:
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "",
                VendorSpecificFlags: new()
            ),
        ];
}
