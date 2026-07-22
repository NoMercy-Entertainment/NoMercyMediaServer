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

namespace NoMercy.Api.DTOs.Management;

public record ManagementActivityDto
{
    [JsonProperty(propertyName: "active_streams")]
    public int ActiveStreams { get; init; }

    [JsonProperty(propertyName: "active_encodes")]
    public int ActiveEncodes { get; init; }

    [JsonProperty(propertyName: "can_interrupt_safely")]
    public bool CanInterruptSafely { get; init; }
}
