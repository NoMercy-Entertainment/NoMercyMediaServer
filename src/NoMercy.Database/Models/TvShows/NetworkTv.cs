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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(propertyName: nameof(NetworkId), additionalPropertyNames: nameof(TvId))]
[Index(propertyName: nameof(NetworkId), additionalPropertyNames: nameof(TvId), IsUnique = true)]
public class NetworkTv : Timestamps
{
    [JsonProperty(propertyName: "network_id")]
    public int NetworkId { get; set; }

    [JsonProperty(propertyName: "network")]
    public Network Network { get; set; } = null!;

    [JsonProperty(propertyName: "tv_id")]
    public int TvId { get; set; }

    [JsonProperty(propertyName: "tv")]
    public Tv Tv { get; set; } = null!;
}
