using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record ProgressDto
{
    [JsonProperty("time")] public int? Time { get; set; }
    [JsonProperty("date")] public DateTime? Date { get; set; }
    
}