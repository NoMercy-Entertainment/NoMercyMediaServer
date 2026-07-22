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
    [JsonProperty(propertyName: "device_id")]
    public required Ulid DeviceId { get; init; }

    [JsonProperty(propertyName: "fingerprint")]
    public required string Fingerprint { get; init; }

    [JsonProperty(propertyName: "name")]
    public required string Name { get; init; }

    [JsonProperty(propertyName: "type")]
    public required string Type { get; init; }

    [JsonProperty(propertyName: "online")]
    public bool Online { get; init; }

    [JsonProperty(propertyName: "lan_ip")]
    public string? LanIp { get; init; }

    [JsonProperty(propertyName: "last_seen_at")]
    public DateTime? LastSeenAt { get; init; }

    // TV-side device-bus client reports app foreground + screen-on state.
    // Phone-side picker uses both flags to skip the Cast SDK CEC wake when
    // the panel is already on with our app on screen.
    [JsonProperty(propertyName: "foreground")]
    public bool Foreground { get; init; }

    [JsonProperty(propertyName: "screen_on")]
    public bool ScreenOn { get; init; }
}
