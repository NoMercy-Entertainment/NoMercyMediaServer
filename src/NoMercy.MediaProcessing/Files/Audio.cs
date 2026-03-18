using Newtonsoft.Json;

namespace NoMercy.MediaProcessing.Files;

public record Audio
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("language")] public string? Language { get; set; } = string.Empty;
}