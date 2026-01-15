using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Core;

/// <summary>
/// Intelligently selects the best video codec based on system hardware capabilities.
/// Prioritizes hardware encoding (GPU) when available, falls back to software encoding.
/// </summary>
public static class CodecSelector
{
    /// <summary>
    /// Selects the appropriate H.264 codec based on available GPU and system capabilities.
    /// Priority: NVIDIA > AMD > Intel > Software
    /// </summary>
    public static Classes.CodecDto SelectH264Codec()
    {
        if (FFmpegHardwareConfig.HasAccelerator("cuda"))
        {
            Logger.Encoder("H.264: Selected h264_nvenc (NVIDIA GPU)", LogEventLevel.Information);
            return VideoCodecs.H264Nvenc;
        }

        if (FFmpegHardwareConfig.HasAccelerator("dxva2") || FFmpegHardwareConfig.HasAccelerator("d3d11va"))
        {
            // Check if it's AMD (DXVA2) or Intel (QSV preferred)
            GpuAccelerator? amdAccelerator = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Vendor == GpuVendor.Amd);
            if (amdAccelerator != null)
            {
                Logger.Encoder("H.264: Selected h264_amf (AMD GPU)", LogEventLevel.Information);
                return VideoCodecs.H264Amf;
            }

            GpuAccelerator? intelAccelerator = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Vendor == GpuVendor.Intel);
            if (intelAccelerator != null)
            {
                Logger.Encoder("H.264: Selected h264_qsv (Intel GPU)", LogEventLevel.Information);
                return VideoCodecs.H264Qsv;
            }
        }

        if (FFmpegHardwareConfig.HasAccelerator("vaapi"))
        {
            GpuVendor? vendor = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Accelerator == "vaapi")?.Vendor;
            if (vendor == GpuVendor.Amd)
            {
                Logger.Encoder("H.264: Selected h264_amf (AMD VAAPI)", LogEventLevel.Information);
                return VideoCodecs.H264Amf;
            }
            if (vendor == GpuVendor.Intel)
            {
                Logger.Encoder("H.264: Selected h264_qsv (Intel VAAPI)", LogEventLevel.Information);
                return VideoCodecs.H264Qsv;
            }
        }

        if (FFmpegHardwareConfig.HasAccelerator("videotoolbox"))
        {
            Logger.Encoder("H.264: Selected h264_videotoolbox (Apple GPU)", LogEventLevel.Information);
            return VideoCodecs.H264Videotoolbox;
        }

        Logger.Encoder("H.264: Selected libx264 (Software - no GPU available)", LogEventLevel.Information);
        return VideoCodecs.H264;
    }

    /// <summary>
    /// Selects the appropriate H.265 codec based on available GPU and system capabilities.
    /// Priority: NVIDIA > AMD > Intel > Software
    /// </summary>
    public static Classes.CodecDto SelectH265Codec()
    {
        if (FFmpegHardwareConfig.HasAccelerator("cuda"))
        {
            Logger.Encoder("H.265: Selected hevc_nvenc (NVIDIA GPU)", LogEventLevel.Information);
            return VideoCodecs.H265Nvenc;
        }

        if (FFmpegHardwareConfig.HasAccelerator("dxva2") || FFmpegHardwareConfig.HasAccelerator("d3d11va"))
        {
            GpuAccelerator? amdAccelerator = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Vendor == GpuVendor.Amd);
            if (amdAccelerator != null)
            {
                Logger.Encoder("H.265: Selected hevc_amf (AMD GPU)", LogEventLevel.Information);
                return VideoCodecs.H265Amf;
            }

            GpuAccelerator? intelAccelerator = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Vendor == GpuVendor.Intel);
            if (intelAccelerator != null)
            {
                Logger.Encoder("H.265: Selected hevc_qsv (Intel GPU)", LogEventLevel.Information);
                return VideoCodecs.H265Qsv;
            }
        }

        if (FFmpegHardwareConfig.HasAccelerator("vaapi"))
        {
            GpuVendor? vendor = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Accelerator == "vaapi")?.Vendor;
            if (vendor == GpuVendor.Amd)
            {
                Logger.Encoder("H.265: Selected hevc_amf (AMD VAAPI)", LogEventLevel.Information);
                return VideoCodecs.H265Amf;
            }
            if (vendor == GpuVendor.Intel)
            {
                Logger.Encoder("H.265: Selected hevc_qsv (Intel VAAPI)", LogEventLevel.Information);
                return VideoCodecs.H265Qsv;
            }
        }

        if (FFmpegHardwareConfig.HasAccelerator("videotoolbox"))
        {
            Logger.Encoder("H.265: Selected hevc_videotoolbox (Apple GPU)", LogEventLevel.Information);
            return VideoCodecs.H265Videotoolbox;
        }

        Logger.Encoder("H.265: Selected libx265 (Software - no GPU available)", LogEventLevel.Information);
        return VideoCodecs.H265;
    }

    /// <summary>
    /// Selects the appropriate VP9 codec based on available GPU and system capabilities.
    /// Priority: NVIDIA > AMD > Software
    /// </summary>
    public static Classes.CodecDto SelectVp9Codec()
    {
        if (FFmpegHardwareConfig.HasAccelerator("cuda"))
        {
            Logger.Encoder("VP9: Selected vp9_nvenc (NVIDIA GPU)", LogEventLevel.Information);
            return VideoCodecs.Vp9Nvenc;
        }

        if (FFmpegHardwareConfig.HasAccelerator("dxva2"))
        {
            GpuAccelerator? amdAccelerator = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Vendor == GpuVendor.Amd);
            if (amdAccelerator != null)
            {
                Logger.Encoder("VP9: Selected vp9_amf (AMD GPU)", LogEventLevel.Information);
                return VideoCodecs.Vp9Amf;
            }
        }

        if (FFmpegHardwareConfig.HasAccelerator("videotoolbox"))
        {
            Logger.Encoder("VP9: Selected vp9_videotoolbox (Apple GPU)", LogEventLevel.Information);
            return VideoCodecs.Vp9Videotoolbox;
        }

        Logger.Encoder("VP9: Selected libvpx-vp9 (Software - no GPU available)", LogEventLevel.Information);
        return VideoCodecs.Vp9;
    }

    /// <summary>
    /// Selects the appropriate AV1 codec based on available GPU and system capabilities.
    /// Priority: NVIDIA > AMD > Intel > Software
    /// </summary>
    public static Classes.CodecDto SelectAv1Codec()
    {
        if (FFmpegHardwareConfig.HasAccelerator("cuda"))
        {
            Logger.Encoder("AV1: Selected av1_nvenc (NVIDIA GPU)", LogEventLevel.Information);
            return VideoCodecs.Av1Nvenc;
        }

        if (FFmpegHardwareConfig.HasAccelerator("dxva2"))
        {
            GpuAccelerator? amdAccelerator = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Vendor == GpuVendor.Amd);
            if (amdAccelerator != null)
            {
                Logger.Encoder("AV1: Selected av1_amf (AMD GPU)", LogEventLevel.Information);
                return VideoCodecs.Av1Amf;
            }

            GpuAccelerator? intelAccelerator = FFmpegHardwareConfig.Accelerators.FirstOrDefault(a => a.Vendor == GpuVendor.Intel);
            if (intelAccelerator != null)
            {
                Logger.Encoder("AV1: Selected av1_qsv (Intel GPU)", LogEventLevel.Information);
                return VideoCodecs.Av1Qsv;
            }
        }

        if (FFmpegHardwareConfig.HasAccelerator("videotoolbox"))
        {
            Logger.Encoder("AV1: Selected av1_videotoolbox (Apple GPU)", LogEventLevel.Information);
            return VideoCodecs.Av1Videotoolbox;
        }

        Logger.Encoder("AV1: Selected librav1e (Software - no GPU available)", LogEventLevel.Information);
        return VideoCodecs.Av1;
    }

    /// <summary>
    /// Generic codec selector that chooses the best available codec family.
    /// Maps simple codec names (h264, h265, vp9, av1) to hardware-accelerated variants when possible.
    /// </summary>
    public static Classes.CodecDto SelectBestCodec(string simpleCodecName)
    {
        return simpleCodecName.ToLower() switch
        {
            "h264" or "h.264" => SelectH264Codec(),
            "h265" or "h.265" or "hevc" => SelectH265Codec(),
            "vp9" => SelectVp9Codec(),
            "av1" => SelectAv1Codec(),
            _ => throw new ArgumentException($"Unknown codec family: {simpleCodecName}")
        };
    }

    /// <summary>
    /// Resolves a codec string (which may be a specific encoder or a family name) to the best available codec.
    /// If the string is already a specific encoder (h264_nvenc, libx264, etc.), it may be overridden if that 
    /// encoder is not available on this system and a fallback is needed.
    /// </summary>
    public static string ResolveBestCodec(string requestedCodec)
    {
        // If it's already a software codec, use it as-is
        if (requestedCodec == VideoCodecs.H264.Value || requestedCodec == VideoCodecs.H264.SimpleValue)
            return SelectH264Codec().Value;

        if (requestedCodec == VideoCodecs.H265.Value || requestedCodec == VideoCodecs.H265.SimpleValue)
            return SelectH265Codec().Value;

        if (requestedCodec == VideoCodecs.Vp9.Value || requestedCodec == VideoCodecs.Vp9.SimpleValue)
            return SelectVp9Codec().Value;

        if (requestedCodec == VideoCodecs.Av1.Value || requestedCodec == VideoCodecs.Av1.SimpleValue)
            return SelectAv1Codec().Value;

        // If it's a GPU-specific codec, check if available; if not, fall back to software equivalent
        if (requestedCodec == VideoCodecs.H264Nvenc.Value || requestedCodec == VideoCodecs.H264Amf.Value || 
            requestedCodec == VideoCodecs.H264Qsv.Value || requestedCodec == VideoCodecs.H264Videotoolbox.Value)
            return SelectH264Codec().Value;

        if (requestedCodec == VideoCodecs.H265Nvenc.Value || requestedCodec == VideoCodecs.H265Amf.Value ||
            requestedCodec == VideoCodecs.H265Qsv.Value || requestedCodec == VideoCodecs.H265Videotoolbox.Value)
            return SelectH265Codec().Value;

        if (requestedCodec == VideoCodecs.Vp9Nvenc.Value || requestedCodec == VideoCodecs.Vp9Amf.Value ||
            requestedCodec == VideoCodecs.Vp9Videotoolbox.Value)
            return SelectVp9Codec().Value;

        if (requestedCodec == VideoCodecs.Av1Nvenc.Value || requestedCodec == VideoCodecs.Av1Amf.Value ||
            requestedCodec == VideoCodecs.Av1Qsv.Value || requestedCodec == VideoCodecs.Av1Videotoolbox.Value)
            return SelectAv1Codec().Value;

        // Unknown codec, return as-is and let FFmpeg error handling catch it
        Logger.Encoder($"Unknown codec requested: {requestedCodec}, returning as-is", LogEventLevel.Warning);
        return requestedCodec;
    }
}
