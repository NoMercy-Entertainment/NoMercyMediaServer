using Newtonsoft.Json;

namespace NoMercy.Networking.Devices;

public sealed record DeviceListItem
{
    [JsonProperty("device_id")]
    public required Ulid DeviceId { get; init; }

    [JsonProperty("fingerprint")]
    public required string Fingerprint { get; init; }

    [JsonProperty("name")]
    public required string Name { get; init; }

    [JsonProperty("type")]
    public required string Type { get; init; }

    [JsonProperty("online")]
    public bool Online { get; init; }

    [JsonProperty("foreground")]
    public bool Foreground { get; init; }

    [JsonProperty("screen_on")]
    public bool ScreenOn { get; init; }

    [JsonProperty("lan_ip")]
    public string? LanIp { get; init; }

    [JsonProperty("last_seen_at")]
    public DateTime? LastSeenAt { get; init; }
}
