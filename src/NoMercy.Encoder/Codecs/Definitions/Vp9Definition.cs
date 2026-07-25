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
                "libvpx-vp9",
                null,
                [],
                // ffmpeg's -profile option for VP9 takes the numeric profile id
                // (0/1/2/3), NOT the "profileN" spelling — libvpx-vp9 rejects the
                // string form ("Unable to parse option value profile0"). The
                // 8/10-bit meaning is documented in the block comment above.
                ["0", "1", "2", "3"],
                [],
                new(0, 63, 33),
                [
                    RateControlMode.Crf,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                true,
                false,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            // Intel Quick Sync Video — vp9_qsv
            // 7 presets (veryfast→veryslow). Quality range 1-51 (NOT 0). Unlimited sessions.
            // Intel-only — no NVIDIA or AMD VP9 hardware encoder exists.
            new(
                "vp9_qsv",
                GpuVendor.Intel,
                ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                [],
                [],
                new(1, 51, 33),
                [
                    RateControlMode.Icq,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                false,
                false,
                int.MaxValue,
                "",
                new()
            ),
            // Intel VAAPI — vp9_vaapi
            // No presets. 4 profiles: numeric 0-3 (mirrors libvpx-vp9 profile numbering).
            // QP 0-255 (VA-API full range). Linux VA-API path. Unlimited sessions.
            new(
                "vp9_vaapi",
                GpuVendor.Intel,
                [],
                ["0", "1", "2", "3"],
                [],
                new(0, 255, 100),
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                false,
                false,
                int.MaxValue,
                "",
                new()
            ),
        ];
}
