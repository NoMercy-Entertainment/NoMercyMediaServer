using Newtonsoft.Json;

namespace NoMercy.Cli.Models;

internal class LogEntryResponse
{
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("message")] public string Message { get; set; } = string.Empty;
    [JsonProperty("color")] public string Color { get; set; } = string.Empty;
    [JsonProperty("threadId")] public int ThreadId { get; set; }
    [JsonProperty("time")] public DateTime Time { get; set; }
    [JsonProperty("level")] public string Level { get; set; } = string.Empty;
}
