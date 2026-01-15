using Newtonsoft.Json;

namespace NoMercy.MediaProcessing.Files;

public record Streams
{
    [JsonProperty("video")] public IEnumerable<Video> Video { get; set; } = new List<Video>();
    [JsonProperty("audio")] public IEnumerable<Audio> Audio { get; set; } = new List<Audio>();
    [JsonProperty("subtitle")] public IEnumerable<Subtitle> Subtitle { get; set; } = new List<Subtitle>();
}