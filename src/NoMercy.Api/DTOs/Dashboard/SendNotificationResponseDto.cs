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

namespace NoMercy.Api.DTOs.Dashboard;

public record SendNotificationResponseDto
{
    [JsonProperty("user_id")]
    public Guid UserId { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("body")]
    public string Body { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    // True when the target user had at least one live SignalR connection on the
    // notification hub at send time — i.e. the push had somewhere to land.
    // False does not mean delivery failed silently; it means there was no
    // live connection to deliver to (same real-time-only semantics as broadcast).
    [JsonProperty("connected")]
    public bool Connected { get; set; }
}
