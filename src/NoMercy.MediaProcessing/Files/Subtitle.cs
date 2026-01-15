using Newtonsoft.Json;

namespace NoMercy.MediaProcessing.Files;

public class Subtitle
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("language")] public string Language { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("ext")] public string Ext { get; set; } = string.Empty;
}