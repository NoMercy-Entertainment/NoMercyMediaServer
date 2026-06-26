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

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzReleaseSearchResponse
{
    [JsonProperty("created")]
    public DateTimeOffset Created { get; set; }

    [JsonProperty("count")]
    public long Count { get; set; }

    [JsonProperty("offset")]
    public long Offset { get; set; }

    [JsonProperty("releases")]
    public MusicBrainzRelease[] Releases { get; set; } = [];
}
