// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Video;

public enum VideoEventType
{
    Null,
    PlayerStateChanged,
    BroadcastUnavailable,
    DeviceStateChanged,
}

public class EventPayload<T>
{
    [JsonProperty(propertyName: "events", NullValueHandling = NullValueHandling.Ignore)]
    public List<T> Events { get; set; } = [];
}

public class PlayerStateEventElement
{
    [JsonProperty(propertyName: "event")]
    public PlayerStateEvent Event { get; set; } = null!;

    [JsonProperty(propertyName: "source")]
    public string Source { get; set; } = null!;

    [JsonProperty(propertyName: "type")]
    public VideoEventType Type { get; set; } = VideoEventType.Null;

    [JsonProperty(propertyName: "user")]
    public User User { get; set; } = null!;
}

public class PlayerStateEvent
{
    [JsonProperty(propertyName: "event_id")]
    public int EventId { get; set; }

    [JsonProperty(propertyName: "state")]
    public VideoPlayerState? State { get; set; }
}

public class BroadcastEventPayload
{
    [JsonProperty(propertyName: "deviceBroadcastStatus")]
    public DeviceBroadcastStatus DeviceBroadcastStatus { get; set; } = new();
}

public class DeviceBroadcastStatus
{
    [JsonProperty(propertyName: "timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty(propertyName: "broadcast_status")]
    public VideoEventType BroadcastStatus { get; set; } = VideoEventType.Null;

    [JsonProperty(propertyName: "device_id")]
    public string DeviceId { get; set; } = null!;
}
