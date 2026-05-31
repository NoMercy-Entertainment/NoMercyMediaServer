using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Dashboard;

public record ServerActivityRequest
{
    [JsonProperty("take")]
    public int? Take { get; set; } = 50;

    [JsonProperty("skip")]
    public int? Skip { get; set; } = 0;

    [JsonProperty("category")]
    public ActivityCategory? Category { get; set; }

    [JsonProperty("user_id")]
    public Guid? UserId { get; set; }

    [JsonProperty("device_id")]
    public Ulid? DeviceId { get; set; }

    [JsonProperty("media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty("from")]
    public DateTime? From { get; set; }

    [JsonProperty("to")]
    public DateTime? To { get; set; }

    [JsonProperty("success")]
    public bool? Success { get; set; }
}
