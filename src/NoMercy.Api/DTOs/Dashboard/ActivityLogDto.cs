using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Dashboard;

public record ActivityLogDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("category")]
    public ActivityCategory Category { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("time")]
    public DateTime Time { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }

    [JsonProperty("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty("media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("error_code")]
    public string? ErrorCode { get; set; }

    [JsonProperty("metadata")]
    public string? Metadata { get; set; }
}
