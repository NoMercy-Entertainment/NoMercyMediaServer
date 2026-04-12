namespace NoMercy.Encoder.V3.Codecs.Definitions;

using NoMercy.Encoder.V3.Hardware;

public class Av1Definition : ICodecDefinition
{
    public VideoCodecType CodecType => VideoCodecType.Av1;

    public EncoderInfo[] Encoders =>
        [
            // Software encoder — libsvtav1 (SVT-AV1, fastest software AV1 encoder)
            // Presets "0"-"13" (14 total — 0=slowest/best, 13=fastest). CRF 0-63, default 35.
            // 10-bit + HDR. Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "libsvtav1",
                RequiredVendor: null,
                Presets: ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13"],
                Profiles: ["main"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 63, Default: 35),
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
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // Software encoder — libaom-av1 (reference AV1 encoder, very slow)
            // Presets "0"-"8" (9 total, maps to cpu-used — 0=slowest, 8=fastest). CRF 0-63.
            // Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "libaom-av1",
                RequiredVendor: null,
                Presets: ["0", "1", "2", "3", "4", "5", "6", "7", "8"],
                Profiles: ["main"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 63, Default: 35),
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
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // Software encoder — librav1e (Rust AV1 encoder)
            // Presets "0"-"10" (11 total — speed levels). QP 0-255 (NOT 0-51!).
            // Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "librav1e",
                RequiredVendor: null,
                Presets: ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"],
                Profiles: ["main"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 255, Default: 100),
                SupportedRateControl: [RateControlMode.Cqp, RateControlMode.Vbr],
                Supports10Bit: true,
                SupportsHdr: true,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "yuv420p10le",
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // NVIDIA NVENC — av1_nvenc
            // Presets p1–p7. Main profile only. CQ/CQP 0-51. 10-bit + HDR.
            // 12 concurrent sessions (driver limit).
            new EncoderInfo(
                FfmpegName: "av1_nvenc",
                RequiredVendor: GpuVendor.Nvidia,
                Presets: ["p1", "p2", "p3", "p4", "p5", "p6", "p7"],
                Profiles: ["main"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 51, Default: 35),
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
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // AMD AMF — av1_amf
            // 4 presets: speed/balanced/quality/high_quality. Main profile.
            // QP 0-255 (AMD AV1 uses full 8-bit range — NOT 0-51!). Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "av1_amf",
                RequiredVendor: GpuVendor.Amd,
                Presets: ["speed", "balanced", "quality", "high_quality"],
                Profiles: ["main"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 255, Default: 100),
                SupportedRateControl:
                [
                    RateControlMode.Cqp,
                    RateControlMode.Cbr,
                    RateControlMode.Vbr,
                    RateControlMode.Qvbr,
                ],
                Supports10Bit: false,
                SupportsHdr: false,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: "",
                VendorSpecificFlags: new Dictionary<string, string>()
            ),
            // Intel Quick Sync Video — av1_qsv
            // 7 presets (veryfast→veryslow). Main profile.
            // Quality range 1-51 (NOT 0). ICQ rate control. Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "av1_qsv",
                RequiredVendor: GpuVendor.Intel,
                Presets: ["veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"],
                Profiles: ["main"],
                Levels: [],
                QualityRange: new QualityRange(Min: 1, Max: 51, Default: 35),
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
            // Intel VAAPI — av1_vaapi
            // No presets. Main profile. QP 0-255 (VA-API full range). Linux VA-API path.
            // Unlimited sessions.
            new EncoderInfo(
                FfmpegName: "av1_vaapi",
                RequiredVendor: GpuVendor.Intel,
                Presets: [],
                Profiles: ["main"],
                Levels: [],
                QualityRange: new QualityRange(Min: 0, Max: 255, Default: 100),
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
            // NOTE: av1_videotoolbox does NOT exist.
            // Apple Silicon decodes AV1 in hardware but does NOT encode it.
        ];
}
