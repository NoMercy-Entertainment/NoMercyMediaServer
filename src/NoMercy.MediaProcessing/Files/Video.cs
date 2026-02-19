using Newtonsoft.Json;

namespace NoMercy.MediaProcessing.Files;

public record Video
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("width")] public int? Width { get; set; }
    [JsonProperty("height")] public int? Height { get; set; }
}