namespace NoMercy.Encoder.V3.Codecs.Definitions;

using NoMercy.Encoder.V3.Hardware;

public class H264Definition : ICodecDefinition
{
    public VideoCodecType CodecType => VideoCodecType.H264;

    public EncoderInfo[] Encoders =>
        [
            // Software encoder — libx264
            // CRF 0-51, default 23. 10 presets. 6 profiles including high10/high422/high444p.
            // Unlimited sessions. 10-bit via high10 profile using yuv420p10le.
            new EncoderInfo(
                FfmpegName: "libx264",
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
                Profiles: ["baseline", "main", "high", "high10", "high422", "high444p"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 51, Default: 23),
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
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // NVIDIA NVENC — h264_nvenc
            // Presets p1–p7 (performance 1=fastest, 7=slowest). No high10 profile — H.264 10-bit
            // unreliable on NVENC. QP range 0-51. CQ/CQP/CBR/VBR (no CRF — software only).
            // 12 concurrent sessions (driver limit). Supports10Bit=false.
            new EncoderInfo(
                FfmpegName: "h264_nvenc",
                RequiredVendor: GpuVendor.Nvidia,
                Presets: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"],
                Profiles: ["baseline", "main", "high"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 51, Default: 23),
                SupportedRateControl:
                [
                    RateControlMode.Cq,
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                ],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: 12,
                PixelFormat10Bit: "",
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // AMD AMF/VCE — h264_amf
            // 3 presets: speed/balanced/quality. 4 profiles including constrained variants.
            // QP 0-51. Rich rate control set including QVBR, HQVBR, HQCBR.
            // Unlimited sessions. Supports10Bit=false. Requires -usage transcoding flag.
            new EncoderInfo(
                FfmpegName: "h264_amf",
                RequiredVendor: GpuVendor.Amd,
                Presets: ["speed", "balanced", "quality"],
                Profiles: ["main", "high", "constrained_baseline", "constrained_high"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 51, Default: 23),
                SupportedRateControl:
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                    RateControlMode.Qvbr,
                    RateControlMode.Hqvbr,
                    RateControlMode.Hqcbr,
                ],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "",
                VendorSpecificFlags: new Dictionary<string, string> { ["-usage"] = "transcoding" }
            ),
            // Intel Quick Sync Video — h264_qsv
            // 7 presets (veryfast→veryslow, no ultrafast/placebo). Profiles baseline/main/high.
            // Quality range 1-51 (NOT 0). ICQ rate control available. Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "h264_qsv",
                RequiredVendor: GpuVendor.Intel,
                Presets: ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                Profiles: ["baseline", "main", "high"],
                Levels: [],
                QualityRange: new QualityRange(Min: 1, Max: 51, Default: 23),
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
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // Intel VAAPI — h264_vaapi
            // No presets. 3 profiles (constrained_baseline/main/high). Unlimited sessions.
            // Linux VA-API path — no preset concept in the driver.
            new EncoderInfo(
                FfmpegName: "h264_vaapi",
                RequiredVendor: GpuVendor.Intel,
                Presets: [],
                Profiles: ["constrained_baseline", "main", "high"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 51, Default: 23),
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
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // Apple VideoToolbox — h264_videotoolbox
            // No presets. Profiles are NUMERIC: 66=Baseline, 77=Main, 100=High.
            // Quality range 0-100 (lower=better for VT). QualityLevel+CBR rate control.
            // No vendor-specific flags (hvc1 tag is HEVC only). Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "h264_videotoolbox",
                RequiredVendor: GpuVendor.Apple,
                Presets: [],
                Profiles: ["66", "77", "100"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 100, Default: 50),
                SupportedRateControl: [RateControlMode.QualityLevel, RateControlMode.Cbr],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "",
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
        ];
}
