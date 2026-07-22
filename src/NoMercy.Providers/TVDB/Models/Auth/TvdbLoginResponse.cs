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
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Auth;

public class TvdbLoginResponse : TvdbResponse<TvdbLogin> { }

public class TvdbLogin
{
    [JsonProperty(propertyName: "token")]
    public string Token { get; set; } = string.Empty;

    [JsonProperty(propertyName: "expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMonths(months: 1);
}
