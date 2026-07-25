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

public class Av1Definition : ICodecDefinition
{
    public VideoCodecType CodecType => VideoCodecType.Av1;

    public EncoderInfo[] Encoders =>
        [
            // Software encoder — libsvtav1 (SVT-AV1, fastest software AV1 encoder)
            // Presets "0"-"13" (14 total — 0=slowest/best, 13=fastest). CRF 0-63, default 35.
            // 10-bit + HDR. Unlimited sessions.
            new(
                "libsvtav1",
                null,
                ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13"],
                ["main"],
                [],
                new(0, 63, 35),
                [
                    RateControlMode.Crf,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                true,
                true,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            // Software encoder — libaom-av1 (reference AV1 encoder, very slow)
            // Presets "0"-"8" (9 total, maps to cpu-used — 0=slowest, 8=fastest). CRF 0-63.
            // Unlimited sessions.
            new(
                "libaom-av1",
                null,
                ["0", "1", "2", "3", "4", "5", "6", "7", "8"],
                ["main"],
                [],
                new(0, 63, 35),
                [
                    RateControlMode.Crf,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                true,
                true,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            // Software encoder — librav1e (Rust AV1 encoder)
            // Presets "0"-"10" (11 total — speed levels). QP 0-255 (NOT 0-51!).
            // Unlimited sessions.
            new(
                "librav1e",
                null,
                ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"],
                ["main"],
                [],
                new(0, 255, 100),
                [RateControlMode.Cqp, RateControlMode.Vbr],
                true,
                true,
                int.MaxValue,
                "yuv420p10le",
                new()
            ),
            // NVIDIA NVENC — av1_nvenc
            // Presets p1–p7. Main profile only. CQ/CQP 0-51. 10-bit + HDR.
            // 12 concurrent sessions (driver limit).
            new(
                "av1_nvenc",
                GpuVendor.Nvidia,
                ["p1", "p2", "p3", "p4", "p5", "p6", "p7"],
                ["main"],
                [],
                new(0, 51, 35),
                [
                    RateControlMode.Cq,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                true,
                true,
                12,
                "yuv420p10le",
                new()
            ),
            // AMD AMF — av1_amf
            // 4 presets: speed/balanced/quality/high_quality. Main profile.
            // QP 0-255 (AMD AV1 uses full 8-bit range — NOT 0-51!). Unlimited sessions.
            new(
                "av1_amf",
                GpuVendor.Amd,
                ["speed", "balanced", "quality", "high_quality"],
                ["main"],
                [],
                new(0, 255, 100),
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                    RateControlMode.Qvbr,
                ],
                false,
                false,
                int.MaxValue,
                "",
                new()
            ),
            // Intel Quick Sync Video — av1_qsv
            // 7 presets (veryfast→veryslow). Main profile.
            // Quality range 1-51 (NOT 0). ICQ rate control. Unlimited sessions.
            new(
                "av1_qsv",
                GpuVendor.Intel,
                ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                ["main"],
                [],
                new(1, 51, 35),
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
            // Intel VAAPI — av1_vaapi
            // No presets. Main profile. QP 0-255 (VA-API full range). Linux VA-API path.
            // Unlimited sessions.
            new(
                "av1_vaapi",
                GpuVendor.Intel,
                [],
                ["main"],
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
            // NOTE: av1_videotoolbox does NOT exist.
            // Apple Silicon decodes AV1 in hardware but does NOT encode it.
        ];
}
