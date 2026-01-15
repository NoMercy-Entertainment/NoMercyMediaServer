using Newtonsoft.Json;

namespace NoMercy.Database;

public class IVideoTrack
{
    [JsonProperty("file")] public string File { get; set; } = null!;
    [JsonProperty("kind")] public string Kind { get; set; } = null!;

    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
    public string? Language { get; set; }
}