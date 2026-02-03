using Newtonsoft.Json;

namespace NoMercy.EncoderV2.Shared.Dtos;

public class Attachment
{
    [JsonProperty("file")] public string Filename { get; set; } = string.Empty;
    [JsonProperty("mimeType")] public string MimeType { get; set; } = string.Empty;
}