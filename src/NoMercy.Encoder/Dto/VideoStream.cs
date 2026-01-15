using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Encoder.Dto;

[Serializable]
public class VideoStream
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("profile")] public string? Profile { get; set; }
    [JsonProperty("codec_name")] public string? CodecName { get; set; }
    [JsonProperty("codec_long_name")] public string? CodecLongName { get; set; }
    [JsonProperty("codec_type")] public CodecType CodecType { get; set; }
    [JsonProperty("time_base")] public string? TimeBase { get; set; }
    [JsonProperty("duration")] public double Duration { get; set; }
    [JsonProperty("size")] public long Size { get; set; }
    
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("coded_width")] public int CodedWidth { get; set; }
    [JsonProperty("coded_height")] public int CodedHeight { get; set; }
    [JsonProperty("closed_captions")] public bool? ClosedCaptions { get; set; }
    [JsonProperty("film_grain")] public bool? FilmGrain { get; set; }
    [JsonProperty("has_b_frames")] public bool? HasBFrames { get; set; }
    [JsonProperty("aspect_ratio")] public string? AspectRatio { get; set; }
    [JsonProperty("pix_fmt")] public string? PixFmt { get; set; }
    [JsonProperty("level")] public long? Level { get; set; }
    [JsonProperty("is_avc")] public bool? IsAvc { get; set; }
    [JsonProperty("color_range")] public string? ColorRange { get; set; }
    [JsonProperty("color_space")] public string? ColorSpace { get; set; }
    [JsonProperty("color_transfer")] public string? ColorTransfer { get; set; }
    [JsonProperty("color_primaries")] public string? ColorPrimaries { get; set; }
    [JsonProperty("chroma_location")] public string? ChromaLocation { get; set; }
    [JsonProperty("avg_frame_rate")] public string? AvgFrameRate { get; set; }
    
    [JsonProperty("tags")] public object Tags { get; set; }

    [JsonProperty("frame_rate")] public int FrameRate { get; set; }

    [JsonProperty("is_default")] public bool IsDefault  { get; set; }
    [JsonProperty("is_dub")] public bool IsDub  { get; set; }
    [JsonProperty("is_forced")] public bool IsForced  { get; set; }
    [JsonProperty("is_hdr")] public bool IsHdr  { get; set; }
    [JsonProperty("bitrate")] public long? BitRate { get; set; }

    public VideoStream(FfprobeSourceDataStream ffprobeSourceDataStream)
    {
        Index = ffprobeSourceDataStream.Index;
        Profile = ffprobeSourceDataStream.Profile;
        CodecName = ffprobeSourceDataStream.CodecName;
        CodecLongName = ffprobeSourceDataStream.CodecLongName;
        CodecType = ffprobeSourceDataStream.CodecType;
        TimeBase = ffprobeSourceDataStream.TimeBase;
        Duration = ffprobeSourceDataStream.Duration.ToSeconds();
        Size = ffprobeSourceDataStream.Tags.TryGetValue("number_of_bytes", out string? numberOfBytes) ? numberOfBytes.ToLong() : 0;
        IsAvc = ffprobeSourceDataStream.IsAvc;
        Width = ffprobeSourceDataStream.Width;
        Height = ffprobeSourceDataStream.Height;
        CodedWidth = ffprobeSourceDataStream.CodedWidth;
        CodedHeight = ffprobeSourceDataStream.CodedHeight;
        ClosedCaptions = ffprobeSourceDataStream.ClosedCaptions;
        FilmGrain = ffprobeSourceDataStream.FilmGrain;
        HasBFrames = ffprobeSourceDataStream.HasBFrames;
        PixFmt = ffprobeSourceDataStream.PixFmt;
        BitRate = ffprobeSourceDataStream.BitRate;
        Level = ffprobeSourceDataStream.Level;
        ColorRange = ffprobeSourceDataStream.ColorRange;
        ColorSpace = ffprobeSourceDataStream.ColorSpace;
        ColorTransfer = ffprobeSourceDataStream.ColorTransfer;
        ColorPrimaries = ffprobeSourceDataStream.ColorPrimaries;
        ChromaLocation = ffprobeSourceDataStream.ChromaLocation;
        AvgFrameRate = ffprobeSourceDataStream.AvgFrameRate;
        FrameRate = ParseFrameRate(ffprobeSourceDataStream.AvgFrameRate ?? string.Empty);
        IsHdr = DetectHdr(ffprobeSourceDataStream);
        AspectRatio = !string.IsNullOrEmpty(ffprobeSourceDataStream.DisplayAspectRatio) 
            ? ffprobeSourceDataStream.DisplayAspectRatio 
            : NumberConverter.NormalizeAspectRatio(Width, Height);
        
        IsDefault = ffprobeSourceDataStream.Disposition.TryGetValue("default", out int defaultValue) && defaultValue == 1;
        IsForced = ffprobeSourceDataStream.Disposition.TryGetValue("forced", out int forcedValue) && forcedValue == 1;
        
        Tags = ffprobeSourceDataStream.Tags;
    }
    
    private static bool DetectHdr(FfprobeSourceDataStream stream)
    {
        // Check HDR transfer functions
        if (!string.IsNullOrEmpty(stream.ColorTransfer))
        {
            string[] hdrTransferFunctions = ["smpte2084", "arib-std-b67", "bt2020-10", "bt2020-12"];
            if (hdrTransferFunctions.Any(tf => stream.ColorTransfer.Contains(tf, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // Check HDR color primaries
        if (!string.IsNullOrEmpty(stream.ColorPrimaries) && 
            stream.ColorPrimaries.Contains("bt2020", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check pixel format for 10-bit or higher
        if (string.IsNullOrEmpty(stream.PixFmt)) return false;
        
        string[] hdrPixelFormats = ["yuv420p10le", "yuv422p10le", "yuv444p10le", "yuv420p12le"];
        return hdrPixelFormats.Any(fmt => stream.PixFmt.Contains(fmt, StringComparison.OrdinalIgnoreCase));
    }
    
    private static int ParseFrameRate(string avgFrameRate)
    {
        if (string.IsNullOrEmpty(avgFrameRate)) return 0;
    
        string[] parts = avgFrameRate.Split('/');
        if (parts.Length != 2 || 
            !int.TryParse(parts[0], out int numerator) || 
            !int.TryParse(parts[1], out int denominator) ||
            denominator == 0)
            return 0;
    
        return numerator / denominator;
    }
}