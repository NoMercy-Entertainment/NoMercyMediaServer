using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record ProgressDto
{
    [JsonProperty("time")] public int? Time { get; set; }
    [JsonProperty("date")] public DateTime? Date { get; set; }
    
}