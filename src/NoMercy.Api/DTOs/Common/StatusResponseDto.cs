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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Common;

public record StatusResponseDto<T>
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = "ok";

    [JsonProperty(propertyName: "data")]
    public T Data { get; set; } = default!;

    [JsonProperty(propertyName: "message")]
    public string? Message
    {
        get;
        set => field = value?.Localize();
    }

    [JsonProperty(propertyName: "args")]
    public dynamic[]? Args { get; set; } = [];
}
