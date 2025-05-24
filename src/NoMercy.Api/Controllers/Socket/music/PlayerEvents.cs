using Newtonsoft.Json;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket.music;

public enum EventType {
    Null,
    PlayerStateChanged,
    BroadcastUnavailable,
    DeviceStateChanged,
}

public class EventPayload<T>
{
    [JsonProperty("events", NullValueHandling = NullValueHandling.Ignore)] public List<T> Events { get; set; } = [];
}

public class PlayerStateEventElement
{
    [JsonProperty("event")] public PlayerStateEvent Event { get; set; } = null!;
    [JsonProperty("source")] public string Source { get; set; } = null!;
    [JsonProperty("type")] public EventType Type { get; set; } = EventType.Null;
    [JsonProperty("user")] public User User { get; set; } = null!;
}

public class PlayerStateEvent
{
    [JsonProperty("event_id")] public int EventId { get; set; }
    [JsonProperty("state")] public PlayerState? State { get; set; }
}

public class BroadcastEventPayload {
    [JsonProperty("deviceBroadcastStatus")] public DeviceBroadcastStatus DeviceBroadcastStatus { get; set; } = new();
}

public class DeviceBroadcastStatus
{
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("broadcast_status")] public EventType BroadcastStatus { get; set; } = EventType.Null;
    [JsonProperty("device_id")] public string DeviceId { get; set; } = null!;
}