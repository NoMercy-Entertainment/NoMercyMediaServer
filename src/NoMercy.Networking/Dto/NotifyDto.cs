using Newtonsoft.Json;

namespace NoMercy.Networking.Dto;

public class NotifyDto
{
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("message")] public string Message { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
}