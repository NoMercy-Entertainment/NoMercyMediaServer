using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;
public record DevicesDto
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("device_id")] public string DeviceId { get; set; } = string.Empty;
    [JsonProperty("browser")] public string Browser { get; set; } = string.Empty;
    [JsonProperty("os")] public string Os { get; set; } = string.Empty;
    [JsonProperty("device")] public string? Device { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("custom_name")] public object? CustomName { get; set; }
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    [JsonProperty("ip")] public string Ip { get; set; } = string.Empty;
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonProperty("activity_logs")] public IEnumerable<ActivityLogDto> ActivityLogs { get; set; } = [];
}