namespace NoMercy.Encoder.Format.Rules;

public static class VideoCodecs
{
    public static readonly Classes.CodecDto H264 = new()
    {
        Name = "H.264",
        Value = "libx264",
        SimpleValue = "h264",
        RequiresGpu = false,
        IsDefault = true
    };

    public static readonly Classes.CodecDto H264Nvenc = new()
    {
        Name = "H.264 (nvenc)",
        Value = "h264_nvenc",
        SimpleValue = "h264_nvenc",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto H264Qsv = new()
    {
        Name = "H.264 (qsv)",
        Value = "h264_qsv",
        SimpleValue = "h264_qsv",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto H264Amf = new()
    {
        Name = "H.264 (amf)",
        Value = "h264_amf",
        SimpleValue = "h264_amf",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto H264Videotoolbox = new()
    {
        Name = "H.264 (videotoolbox)",
        Value = "h264_videotoolbox",
        SimpleValue = "h264_videotoolbox",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto H265 = new()
    {
        Name = "H.265",
        Value = "libx265",
        SimpleValue = "h265",
        RequiresGpu = false,
        IsDefault = true
    };

    public static readonly Classes.CodecDto H265Nvenc = new()
    {
        Name = "H.265 (nvenc)",
        Value = "hevc_nvenc",
        SimpleValue = "hevc_nvenc",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto H265Qsv = new()
    {
        Name = "H.265 (qsv)",
        Value = "hevc_qsv",
        SimpleValue = "hevc_qsv",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto H265Amf = new()
    {
        Name = "H.265 (amf)",
        Value = "hevc_amf",
        SimpleValue = "hevc_amf",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto H265Videotoolbox = new()
    {
        Name = "H.265 (videotoolbox)",
        Value = "hevc_videotoolbox",
        SimpleValue = "hevc_videotoolbox",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Vp9 = new()
    {
        Name = "VP9",
        Value = "vp9",
        SimpleValue = "vp9",
        RequiresGpu = false,
        IsDefault = true
    };

    public static readonly Classes.CodecDto Vp9Nvenc = new()
    {
        Name = "VP9 (nvenc)",
        Value = "vp9_nvenc",
        SimpleValue = "vp9_nvenc",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Vp9Qsv = new()
    {
        Name = "VP9 (qsv)",
        Value = "vp9_qsv",
        SimpleValue = "vp9_qsv",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Vp9Amf = new()
    {
        Name = "VP9 (amf)",
        Value = "vp9_amf",
        SimpleValue = "vp9_amf",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Vp9Videotoolbox = new()
    {
        Name = "VP9 (videotoolbox)",
        Value = "vp9_videotoolbox",
        SimpleValue = "vp9_videotoolbox",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Av1 = new()
    {
        Name = "av1",
        Value = "librav1e",
        SimpleValue = "librav1e",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Av1Nvenc = new()
    {
        Name = "av1 (nvenc)",
        Value = "av1_nvenc",
        SimpleValue = "av1_nvenc",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Av1Qsv = new()
    {
        Name = "av1 (qsv)",
        Value = "av1_qsv",
        SimpleValue = "av1_qsv",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Av1Amf = new()
    {
        Name = "av1 (amf)",
        Value = "av1_amf",
        SimpleValue = "av1_amf",
        RequiresGpu = true,
        IsDefault = false
    };

    public static readonly Classes.CodecDto Av1Videotoolbox = new()
    {
        Name = "av1 (videotoolbox)",
        Value = "av1_videotoolbox",
        SimpleValue = "av1_videotoolbox",
        RequiresGpu = true,
        IsDefault = false
    };
}