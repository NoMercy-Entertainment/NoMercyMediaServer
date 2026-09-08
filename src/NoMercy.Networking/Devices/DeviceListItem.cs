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

    [JsonProperty("lan_ip")]
    public string? LanIp { get; init; }

    [JsonProperty("last_seen_at")]
    public DateTime? LastSeenAt { get; init; }

    // TV-side device-bus client reports app foreground + screen-on state.
    // Phone-side picker uses both flags to skip the Cast SDK CEC wake when
    // the panel is already on with our app on screen.
    [JsonProperty("foreground")]
    public bool Foreground { get; init; }

    [JsonProperty("screen_on")]
    public bool ScreenOn { get; init; }

    // True when a real Google Cast mDNS announcement (_googlecast._tcp) was
    // seen from this device's own LanIp, independent of Online (the
    // websocket-bus flag). A device can be offline (app not running) and
    // still CastReachable=true — it is a live Chromecast target on the LAN,
    // not truly gone, and a wake attempt is worth trying. Additive field:
    // older clients that don't read it keep working unchanged.
    [JsonProperty("cast_reachable")]
    public bool CastReachable { get; init; }
}
