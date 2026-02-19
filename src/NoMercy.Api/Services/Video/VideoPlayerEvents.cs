using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Video;

public enum VideoEventType
{
    Null,
    PlayerStateChanged,
    BroadcastUnavailable,
    DeviceStateChanged
}

public class EventPayload<T>
{
    [JsonProperty("events", NullValueHandling = NullValueHandling.Ignore)]
    public List<T> Events { get; set; } = [];
}

public class PlayerStateEventElement
{
    [JsonProperty("event")] public PlayerStateEvent Event { get; set; } = null!;
    [JsonProperty("source")] public string Source { get; set; } = null!;
    [JsonProperty("type")] public VideoEventType Type { get; set; } = VideoEventType.Null;
    [JsonProperty("user")] public User User { get; set; } = null!;
}

public class PlayerStateEvent
{
    [JsonProperty("event_id")] public int EventId { get; set; }
    [JsonProperty("state")] public VideoPlayerState? State { get; set; }
}

public class BroadcastEventPayload
{
    [JsonProperty("deviceBroadcastStatus")]
    public DeviceBroadcastStatus DeviceBroadcastStatus { get; set; } = new();
}

public class DeviceBroadcastStatus
{
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("broadcast_status")] public VideoEventType BroadcastStatus { get; set; } = VideoEventType.Null;
    [JsonProperty("device_id")] public string DeviceId { get; set; } = null!;
}