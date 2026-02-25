using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serilog.Events;

namespace NoMercy.NmSystem.Dto;

public class LogEntry
{
    [JsonProperty("type")]
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("color")]
    [JsonPropertyName("Color")]
    public string Color { get; set; } = string.Empty;

    [JsonProperty("threadId")]
    [JsonPropertyName("ThreadId")]
    public int ThreadId { get; set; }

    [JsonProperty("time")]
    [JsonPropertyName("@t")]
    public DateTime Time { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public dynamic LogMessage { get; set; } = default!;

    [NotMapped]
    [JsonProperty("message")]
    [JsonPropertyName("Message")]
    public string Message
    {
        get => LogMessage;
        set => LogMessage = value;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public LogEventLevel LogLevel { get; set; }

    [NotMapped]
    [JsonProperty("level")]
    [JsonPropertyName("Level")]
    public string Level
    {
        get => LogLevel.ToString();
        set => LogLevel = Enum.Parse<LogEventLevel>(value);
    }
}