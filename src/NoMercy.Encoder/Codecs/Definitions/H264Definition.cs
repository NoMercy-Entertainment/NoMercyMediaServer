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

public class H264Definition : ICodecDefinition
{
    public VideoCodecType CodecType => VideoCodecType.H264;

    public EncoderInfo[] Encoders =>
        [
            // Software encoder — libx264
            // CRF 0-51, default 23. 10 presets. 6 profiles including high10/high422/high444p.
            // Unlimited sessions. 10-bit via high10 profile using yuv420p10le.
            new(
                "libx264",
                null,
                [
                    "ultrafast",
                    "superfast",
                    "veryfast",
                    "faster",
                    "fast",
                    "medium",
                    "slow",
                    "slower",
                    "veryslow",
                    "placebo",
                ],
                ["baseline", "main", "high", "high10", "high422", "high444p"],
                [],
                new(0, 51, 23),
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
            // NVIDIA NVENC — h264_nvenc
            // Presets p1–p7 (performance 1=fastest, 7=slowest). No high10 profile — H.264 10-bit
            // unreliable on NVENC. QP range 0-51. CQ/CQP/CBR/VBR (no CRF — software only).
            // 12 concurrent sessions (driver limit). Supports10Bit=false.
            new(
                "h264_nvenc",
                GpuVendor.Nvidia,
                ["p1", "p2", "p3", "p4", "p5", "p6", "p7"],
                ["baseline", "main", "high"],
                [],
                new(0, 51, 23),
                [
                    RateControlMode.Cq,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                false,
                false,
                12,
                "",
                new()
            ),
            // AMD AMF/VCE — h264_amf
            // 3 presets: speed/balanced/quality. 4 profiles including constrained variants.
            // QP 0-51. Rich rate control set including QVBR, HQVBR, HQCBR.
            // Unlimited sessions. Supports10Bit=false. Requires -usage transcoding flag.
            new(
                "h264_amf",
                GpuVendor.Amd,
                ["speed", "balanced", "quality"],
                ["main", "high", "constrained_baseline", "constrained_high"],
                [],
                new(0, 51, 23),
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                    RateControlMode.Qvbr,
                    RateControlMode.Hqvbr,
                    RateControlMode.Hqcbr,
                ],
                false,
                false,
                int.MaxValue,
                "",
                new() { ["-usage"] = "transcoding" }
            ),
            // Intel Quick Sync Video — h264_qsv
            // 7 presets (veryfast→veryslow, no ultrafast/placebo). Profiles baseline/main/high.
            // Quality range 1-51 (NOT 0). ICQ rate control available. Unlimited sessions.
            new(
                "h264_qsv",
                GpuVendor.Intel,
                ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                ["baseline", "main", "high"],
                [],
                new(1, 51, 23),
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
            // Intel VAAPI — h264_vaapi
            // No presets. 3 profiles (constrained_baseline/main/high). Unlimited sessions.
            // Linux VA-API path — no preset concept in the driver.
            new(
                "h264_vaapi",
                GpuVendor.Intel,
                [],
                ["constrained_baseline", "main", "high"],
                [],
                new(0, 51, 23),
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
            // Apple VideoToolbox — h264_videotoolbox
            // No presets. Profiles are NUMERIC: 66=Baseline, 77=Main, 100=High.
            // Quality range 0-100 (lower=better for VT). QualityLevel+CBR rate control.
            // No vendor-specific flags (hvc1 tag is HEVC only). Unlimited sessions.
            new(
                "h264_videotoolbox",
                GpuVendor.Apple,
                [],
                ["66", "77", "100"],
                [],
                new(0, 100, 50),
                [RateControlMode.QualityLevel, RateControlMode.Cbr],
                false,
                false,
                int.MaxValue,
                "",
                new()
            ),
        ];
}
