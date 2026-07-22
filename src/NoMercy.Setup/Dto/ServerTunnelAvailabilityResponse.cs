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

namespace NoMercy.Setup.Dto;

public class ServerTunnelAvailabilityResponse
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = null!;

    [JsonProperty(propertyName: "message")]
    public string? Message { get; set; }

    [JsonProperty(propertyName: "allowed")]
    public bool Allowed { get; set; }

    [JsonProperty(propertyName: "token")]
    public string? Token { get; set; }
}
