using Newtonsoft.Json;

namespace NoMercy.Encoder.Dto;

[Serializable]
public class Attachment
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("codec_name")] public string? CodecName { get; set; }
    [JsonProperty("codec_long_name")] public string? CodecLongName { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("mimetype")] public string? Mimetype { get; set; }
    [JsonProperty("size")] public int Size { get; set; }
    
    public Attachment(FfprobeSourceDataStream ffprobeSourceDataStream)
    {
        Index = ffprobeSourceDataStream.Index;
        Filename = ffprobeSourceDataStream.Tags.TryGetValue("filename", out string? filename) ? filename : null;
        Title = ffprobeSourceDataStream.Tags.TryGetValue("title", out string? title) ? title : null;
        Mimetype = ffprobeSourceDataStream.Tags.TryGetValue("mimetype", out string? mimeType) ? mimeType :  "und";
        Size = ffprobeSourceDataStream.ExtradataSize;
        CodecName = ffprobeSourceDataStream.CodecName;
        CodecLongName = ffprobeSourceDataStream.CodecLongName;
    }

}