using Newtonsoft.Json;

namespace NoMercy.Database;

public class IVideoTrack
{
    [JsonProperty("file")] public string? File { get; set; } = string.Empty;
    [JsonProperty("kind")] public string? Kind { get; set; } = string.Empty;

    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
    public string? Language { get; set; }
}