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

namespace NoMercy.Api.Services.Music;

public enum MusicEventType
{
    Null,
    PlayerStateChanged,
    BroadcastUnavailable,
    DeviceStateChanged,
}

public class EventPayload<T>
{
    [JsonProperty("events", NullValueHandling = NullValueHandling.Ignore)]
    public List<T> Events { get; set; } = [];
}

public class PlayerStateEventElement
{
    [JsonProperty("event")]
    public PlayerStateEvent Event { get; set; } = null!;

    [JsonProperty("source")]
    public string Source { get; set; } = null!;

    [JsonProperty("type")]
    public MusicEventType Type { get; set; } = MusicEventType.Null;

    [JsonProperty("user")]
    public User User { get; set; } = null!;
}

public class PlayerStateEvent
{
    [JsonProperty("event_id")]
    public int EventId { get; set; }

    [JsonProperty("state")]
    public MusicPlayerState? State { get; set; }
}

public class BroadcastEventPayload
{
    [JsonProperty("deviceBroadcastStatus")]
    public DeviceBroadcastStatus DeviceBroadcastStatus { get; set; } = new();
}

public class DeviceBroadcastStatus
{
    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("broadcast_status")]
    public MusicEventType BroadcastStatus { get; set; } = MusicEventType.Null;

    [JsonProperty("device_id")]
    public string DeviceId { get; set; } = null!;
}
