using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Serilog.Events;

namespace NoMercy.NmSystem.Dto;

public class LogEntry
{
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    // [JsonProperty("message")] public string Message { get; set; } = string.Empty;
    [JsonProperty("color")] public string Color { get; set; } = string.Empty;
    [JsonProperty("threadId")] public int ThreadId { get; set; }
    [JsonProperty("time")] public DateTime Time { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public dynamic LogMessage { get; set; } = default!;

    [NotMapped]
    [JsonProperty("message")]
    public string Message
    {
        get => LogMessage;
        set => LogMessage = value;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public LogEventLevel LogLevel { get; set; }

    [NotMapped]
    [JsonProperty("level")]
    public string Level
    {
        get => LogLevel.ToString();
        set => LogLevel = Enum.Parse<LogEventLevel>(value);
    }
}