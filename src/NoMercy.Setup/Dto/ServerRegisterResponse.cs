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

namespace NoMercy.Setup.Dto;

public class ServerRegisterResponse
{
    [JsonProperty("data")]
    public ServerRegisterResponseData Data { get; set; } = new();
}

public class ServerRegisterResponseData
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonProperty("user")]
    public User User { get; set; } = new();
}
