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
                FfmpegName: "libx265",
                RequiredVendor: null,
                Presets:
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
                Profiles: ["main", "main10", "main12", "main422-10", "main444-10"],
                Levels: [],
                QualityRange: new(Min: 0, Max: 51, Default: 28),
                SupportedRateControl:
                [
                    RateControlMode.Crf,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: true,
                SupportsHdr: true,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
            ),
            // NVIDIA NVENC — hevc_nvenc
            // Presets p1–p7. Profiles main/main10/rext.
            // CQ 0-51. 10-bit + HDR via main10/rext. 12 concurrent sessions (driver limit).
            new(
                FfmpegName: "hevc_nvenc",
                RequiredVendor: GpuVendor.Nvidia,
                Presets: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"],
                Profiles: ["main", "main10", "rext"],
                Levels: [],
                QualityRange: new(Min: 0, Max: 51, Default: 28),
                SupportedRateControl:
                [
                    RateControlMode.Cq,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: true,
                SupportsHdr: true,
                MaxConcurrentSessions: 12,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
            ),
            // AMD AMF/VCE — hevc_amf
            // 3 presets: speed/balanced/quality. Profiles main/main10.
            // QP 0-51. 10-bit + HDR via main10. Unlimited sessions. Requires -usage transcoding flag.
            new(
                FfmpegName: "hevc_amf",
                RequiredVendor: GpuVendor.Amd,
                Presets: ["speed", "balanced", "quality"],
                Profiles: ["main", "main10"],
                Levels: [],
                QualityRange: new(Min: 0, Max: 51, Default: 28),
                SupportedRateControl:
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                    RateControlMode.Qvbr,
                    RateControlMode.Hqvbr,
                    RateControlMode.Hqcbr,
                ],
                Supports10Bit: true,
                SupportsHdr: true,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new() { ["-usage"] = "transcoding" }
            ),
            // Intel Quick Sync Video — hevc_qsv
            // 7 presets (veryfast→veryslow). Profiles main/main10/mainsp/rext/scc.
            // Quality range 1-51 (NOT 0). ICQ rate control available. Unlimited sessions.
            new(
                FfmpegName: "hevc_qsv",
                RequiredVendor: GpuVendor.Intel,
                Presets: ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                Profiles: ["main", "main10", "mainsp", "rext", "scc"],
                Levels: [],
                QualityRange: new(Min: 1, Max: 51, Default: 28),
                SupportedRateControl:
                [
                    RateControlMode.Icq,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: true,
                SupportsHdr: true,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
            ),
            // Intel VAAPI — hevc_vaapi
            // No presets. Profiles main/main10. Linux VA-API path.
            // 10-bit + HDR via main10 profile.
            new(
                FfmpegName: "hevc_vaapi",
                RequiredVendor: GpuVendor.Intel,
                Presets: [],
                Profiles: ["main", "main10"],
                Levels: [],
                QualityRange: new(Min: 0, Max: 51, Default: 28),
                SupportedRateControl:
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: true,
                SupportsHdr: true,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new()
            ),
            // Apple VideoToolbox — hevc_videotoolbox
            // No presets. Profiles are NUMERIC: "1" = Main, "2" = Main10.
            // Quality range 0-100. REQUIRES -tag:v hvc1 for broad client compatibility.
            // Unlimited sessions.
            new(
                FfmpegName: "hevc_videotoolbox",
                RequiredVendor: GpuVendor.Apple,
                Presets: [],
                Profiles: ["1", "2"],
                Levels: [],
                QualityRange: new(Min: 0, Max: 100, Default: 50),
                SupportedRateControl: [RateControlMode.QualityLevel, RateControlMode.Cbr],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "",
                VendorSpecificFlags: new() { ["-tag:v"] = "hvc1" }
            ),
        ];
}
