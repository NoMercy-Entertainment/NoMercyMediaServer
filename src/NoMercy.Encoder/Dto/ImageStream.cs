using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Encoder.Dto;

public class ImageStream
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("profile")] public string Profile { get; set; }
    [JsonProperty("codec_name")] public string CodecName { get; set; }
    [JsonProperty("codec_long_name")] public string CodecLongName { get; set; }
    [JsonProperty("codec_type")] public CodecType CodecType { get; set; }
    [JsonProperty("size")] public long Size { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("mimetype")] public string? Mimetype { get; set; }
    
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("coded_width")] public int CodedWidth { get; set; }
    [JsonProperty("coded_height")] public int CodedHeight { get; set; }
    [JsonProperty("aspect_ratio")] public string DisplayAspectRatio => NumberConverter.NormalizeAspectRatio(Width, Height);
    
    public ImageStream(FfprobeSourceDataStream ffprobeSourceDataStream)
    {
        Index = ffprobeSourceDataStream.Index;
        Profile = ffprobeSourceDataStream.Profile;
        CodecName = ffprobeSourceDataStream.CodecName;
        CodecLongName = ffprobeSourceDataStream.CodecLongName;
        CodecType = ffprobeSourceDataStream.CodecType;
        Size = ffprobeSourceDataStream.Tags.NumberOfBytes;
        Width = ffprobeSourceDataStream.Width;
        Height = ffprobeSourceDataStream.Height;
        CodedWidth = ffprobeSourceDataStream.CodedWidth;
        CodedHeight = ffprobeSourceDataStream.CodedHeight;
        Filename = ffprobeSourceDataStream.Tags.Filename;
        Title = ffprobeSourceDataStream.Tags.Title;
        Mimetype = ffprobeSourceDataStream.Tags.MimeType;
    }
}