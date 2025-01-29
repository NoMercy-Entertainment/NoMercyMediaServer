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

    public static readonly Classes.CodecDto Av1 = new()
    {
        Name = "av1",
        Value = "av1",
        SimpleValue = "av1",
        RequiresGpu = true,
        IsDefault = false
    };
}