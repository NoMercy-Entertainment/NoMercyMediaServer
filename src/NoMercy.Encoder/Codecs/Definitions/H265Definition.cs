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

public class H265Definition : ICodecDefinition
{
    public VideoCodecType CodecType => VideoCodecType.H265;

    public EncoderInfo[] Encoders =>
        [
            // Software encoder — libx265
            // CRF 0-51, default 28. 10 presets. 5 profiles including main10/main12/main422-10/main444-10.
            // Unlimited sessions. 10-bit and HDR supported via main10/main12 profiles.
            new(
                "libx265",
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
                ["main", "main10", "main12", "main422-10", "main444-10"],
                [],
                new(0, 51, 28),
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
            // NVIDIA NVENC — hevc_nvenc
            // Presets p1–p7. Profiles main/main10/rext.
            // CQ 0-51. 10-bit + HDR via main10/rext. 12 concurrent sessions (driver limit).
            new(
                "hevc_nvenc",
                GpuVendor.Nvidia,
                ["p1", "p2", "p3", "p4", "p5", "p6", "p7"],
                ["main", "main10", "rext"],
                [],
                new(0, 51, 28),
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
            // AMD AMF/VCE — hevc_amf
            // 3 presets: speed/balanced/quality. Profiles main/main10.
            // QP 0-51. 10-bit + HDR via main10. Unlimited sessions. Requires -usage transcoding flag.
            new(
                "hevc_amf",
                GpuVendor.Amd,
                ["speed", "balanced", "quality"],
                ["main", "main10"],
                [],
                new(0, 51, 28),
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                    RateControlMode.Qvbr,
                    RateControlMode.Hqvbr,
                    RateControlMode.Hqcbr,
                ],
                true,
                true,
                int.MaxValue,
                "yuv420p10le",
                new() { ["-usage"] = "transcoding" }
            ),
            // Intel Quick Sync Video — hevc_qsv
            // 7 presets (veryfast→veryslow). Profiles main/main10/mainsp/rext/scc.
            // Quality range 1-51 (NOT 0). ICQ rate control available. Unlimited sessions.
            new(
                "hevc_qsv",
                GpuVendor.Intel,
                ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                ["main", "main10", "mainsp", "rext", "scc"],
                [],
                new(1, 51, 28),
                [
                    RateControlMode.Icq,
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
            // Intel VAAPI — hevc_vaapi
            // No presets. Profiles main/main10. Linux VA-API path.
            // 10-bit + HDR via main10 profile.
            new(
                "hevc_vaapi",
                GpuVendor.Intel,
                [],
                ["main", "main10"],
                [],
                new(0, 51, 28),
                [
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
            // Apple VideoToolbox — hevc_videotoolbox
            // No presets. Profiles are NUMERIC: "1" = Main, "2" = Main10.
            // Quality range 0-100. REQUIRES -tag:v hvc1 for broad client compatibility.
            // Unlimited sessions.
            new(
                "hevc_videotoolbox",
                GpuVendor.Apple,
                [],
                ["1", "2"],
                [],
                new(0, 100, 50),
                [RateControlMode.QualityLevel, RateControlMode.Cbr],
                false,
                false,
                int.MaxValue,
                "",
                new() { ["-tag:v"] = "hvc1" }
            ),
        ];
}
